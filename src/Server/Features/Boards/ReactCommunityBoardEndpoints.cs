using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Server.Data;
using Server.Services;
using Server.Services.Notifications;

namespace Server.Features.Boards;

/// <summary>Read-only React projections over the canonical board records.</summary>
public sealed class ListReactCommunityBoardsEndpoint(ApplicationDbContext db)
    : EndpointWithoutRequest<List<ReactCommunityBoard>>
{
    public override void Configure()
    {
        Get("/community/boards");
        AllowAnonymous();
    }

    public async override Task HandleAsync(CancellationToken ct)
    {
        var boards = await db.Boards.AsNoTracking()
            .OrderBy(board => board.Name)
            .Select(board => new ReactCommunityBoard(board.Id.ToString(), board.Name, board.IsLocked))
            .ToListAsync(ct);
        await Send.OkAsync(boards, ct);
    }
}

public sealed class GetReactCommunityBoardEndpoint(ApplicationDbContext db)
    : EndpointWithoutRequest<ReactCommunityBoard>
{
    public override void Configure()
    {
        Get("/community/boards/{boardId}");
        AllowAnonymous();
    }

    public async override Task HandleAsync(CancellationToken ct)
    {
        if (!int.TryParse(Route<string>("boardId"), out var boardId))
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        var board = await db.Boards.AsNoTracking().SingleOrDefaultAsync(item => item.Id == boardId, ct);
        if (board is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        await Send.OkAsync(new ReactCommunityBoard(board.Id.ToString(), board.Name, board.IsLocked), ct);
    }
}

public sealed record ReactCommunityBoard(string Id, string Title, bool Locked);

/// <summary>React write projections. They retain the canonical board entities and policy services.</summary>
public sealed class CreateReactCommunityPostEndpoint(
    ApplicationDbContext db, ICaptchaVerificationService captcha, ILinkContentGuard linkGuard, IUserRatingService ratings, INotificationService notifications)
    : Endpoint<ReactCreatePostRequest, ReactCommunityPost>
{
    public override void Configure() { Post("/community/boards/{boardId}/posts"); Roles("User", "SuperAdmin", "Moderator"); }
    public override async Task HandleAsync(ReactCreatePostRequest req, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(Route<string>("boardId"), out var boardId) || string.IsNullOrWhiteSpace(userId)) { await Send.UnauthorizedAsync(ct); return; }
        var result = await new BoardMutationService(db, captcha, linkGuard, ratings, notifications).CreatePostAsync(boardId, userId, req.Title, req.Content, BoardPostType.Issue, req.CaptchaToken, HttpContext.Connection.RemoteIpAddress?.ToString(), ct);
        if (result.Value is null) { if (result.StatusCode == StatusCodes.Status404NotFound) await Send.NotFoundAsync(ct); else await Send.ResponseAsync(default!, result.StatusCode, ct); return; }
        var post = result.Value;
        await Send.OkAsync(new ReactCommunityPost(post.Id, post.BoardId, post.Title, post.Content, 0), ct);
    }
}

public sealed class CreateReactCommunityCommentEndpoint(
    ApplicationDbContext db, ICaptchaVerificationService captcha, ILinkContentGuard linkGuard, IUserRatingService ratings, INotificationService notifications)
    : Endpoint<ReactCreateCommentRequest, ReactCommunityComment>
{
    public override void Configure() { Post("/community/boards/posts/{postId}/comments"); Roles("User", "SuperAdmin", "Moderator"); }
    public override async Task HandleAsync(ReactCreateCommentRequest req, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(Route<string>("postId"), out var postId) || string.IsNullOrWhiteSpace(userId)) { await Send.UnauthorizedAsync(ct); return; }
        var result = await new BoardMutationService(db, captcha, linkGuard, ratings, notifications).CreateCommentAsync(postId, userId, req.Content, req.ParentCommentId, req.CaptchaToken, HttpContext.Connection.RemoteIpAddress?.ToString(), ct);
        if (result.Value is null) { if (result.StatusCode == StatusCodes.Status404NotFound) await Send.NotFoundAsync(ct); else await Send.ResponseAsync(default!, result.StatusCode, ct); return; }
        var comment = result.Value;
        await Send.OkAsync(new ReactCommunityComment(comment.Id, comment.PostId, comment.ParentCommentId, comment.Content, 0), ct);
    }
}

public abstract class ReactVoteEndpoint(ApplicationDbContext db, ICaptchaVerificationService captcha, ILinkContentGuard linkGuard, IUserRatingService ratings, INotificationService notifications) : Endpoint<ReactVoteRequest, ReactVoteResponse>
{
    protected async Task VoteAsync(bool isPost, int id, int value, CancellationToken ct)
    {
        var user = User.FindFirstValue(ClaimTypes.NameIdentifier); if (string.IsNullOrWhiteSpace(user)) { await Send.UnauthorizedAsync(ct); return; }
        value = value is 1 or -1 ? value : 0;
        if (isPost) { var result = await new BoardMutationService(db, captcha, linkGuard, ratings, notifications).VotePostAsync(id, user, value, ct); if (result.Value is null) { await Send.NotFoundAsync(ct); return; } await Send.OkAsync(new ReactVoteResponse(result.Value.Upvotes - result.Value.Downvotes), ct); return; }
        var commentResult = await new BoardMutationService(db, captcha, linkGuard, ratings, notifications).VoteCommentAsync(id, user, value, ct); if (commentResult.Value is null) { await Send.NotFoundAsync(ct); return; } await Send.OkAsync(new ReactVoteResponse(commentResult.Value.Upvotes - commentResult.Value.Downvotes), ct);
    }
}
public sealed class VoteReactCommunityPostEndpoint(ApplicationDbContext db, ICaptchaVerificationService captcha, ILinkContentGuard linkGuard, IUserRatingService ratings, INotificationService notifications) : ReactVoteEndpoint(db, captcha, linkGuard, ratings, notifications)
{
    public override void Configure() { Post("/community/boards/posts/{postId}/vote"); Roles("User", "SuperAdmin"); }
    public override Task HandleAsync(ReactVoteRequest req, CancellationToken ct) => VoteAsync(true, Route<int>("postId"), req.Value, ct);
}
public sealed class VoteReactCommunityCommentEndpoint(ApplicationDbContext db, ICaptchaVerificationService captcha, ILinkContentGuard linkGuard, IUserRatingService ratings, INotificationService notifications) : ReactVoteEndpoint(db, captcha, linkGuard, ratings, notifications)
{
    public override void Configure() { Post("/community/boards/comments/{commentId}/vote"); Roles("User", "SuperAdmin"); }
    public override Task HandleAsync(ReactVoteRequest req, CancellationToken ct) => VoteAsync(false, Route<int>("commentId"), req.Value, ct);
}
public sealed class SetReactCommunityBoardLockedEndpoint(ApplicationDbContext db, Server.Services.IAuthorizationService authorization) : Endpoint<ReactLockRequest, EmptyResponse>
{
    public override void Configure() { Post("/community/boards/{boardId}/moderation/lock"); Roles("User", "SuperAdmin", "Moderator"); }
    public override async Task HandleAsync(ReactLockRequest req, CancellationToken ct) { var user = User.FindFirstValue(ClaimTypes.NameIdentifier); if (string.IsNullOrWhiteSpace(user)) { await Send.UnauthorizedAsync(ct); return; } var result = await BoardMutationService.SetBoardLockedAsync(db, Route<int>("boardId"), user, req.Locked, authorization, ct); if (result.Value is null && result.StatusCode == StatusCodes.Status404NotFound) { await Send.NotFoundAsync(ct); return; } if (result.Value is null) { await Send.ForbiddenAsync(ct); return; } await Send.OkAsync(ct); }
}

public sealed record ReactCreatePostRequest(string Title, string Content, string? CaptchaToken = null);
public sealed record ReactCreateCommentRequest(string Content, int? ParentCommentId = null, string? CaptchaToken = null);
public sealed record ReactVoteRequest(int Value);
public sealed record ReactLockRequest(bool Locked);
public sealed record ReactCommunityPost(int Id, int BoardId, string Title, string Content, int Score);
public sealed record ReactCommunityComment(int Id, int PostId, int? ParentCommentId, string Content, int Score);
public sealed record ReactVoteResponse(int Score);
