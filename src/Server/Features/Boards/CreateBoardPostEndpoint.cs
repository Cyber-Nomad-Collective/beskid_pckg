using System.Security.Claims;
using FastEndpoints;
using Server.Data;
using Server.Services;
using Server.Services.Notifications;

namespace Server.Features.Boards;

public sealed class CreateBoardPostEndpoint : Endpoint<CreateBoardPostRequest, CreateBoardPostResponse>
{
    public ApplicationDbContext Db { get; set; } = default!;
    public IUserRatingService RatingService { get; set; } = default!;
    public ICaptchaVerificationService Captcha { get; set; } = default!;
    public ILinkContentGuard LinkGuard { get; set; } = default!;
    public INotificationService Notifications { get; set; } = default!;

    public override void Configure()
    {
        Post("/boards/{boardId}/posts");
        Roles("User", "SuperAdmin", "Moderator");
    }

    public override async Task HandleAsync(CreateBoardPostRequest req, CancellationToken ct)
    {
        var boardId = Route<int>("boardId");
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        var result = await new BoardMutationService(Db, Captcha, LinkGuard, RatingService, Notifications).CreatePostAsync(boardId, userId, req.Title, req.Content, req.PostType, req.CaptchaToken, HttpContext.Connection.RemoteIpAddress?.ToString(), ct);
        if (result.Value is not null) { await Send.OkAsync(new CreateBoardPostResponse(true, "Post created successfully.", result.Value.Id), ct); return; }
        if (result.StatusCode == StatusCodes.Status404NotFound) { await Send.NotFoundAsync(ct); return; }
        await Send.ResponseAsync(new CreateBoardPostResponse(false, result.Message!), result.StatusCode, ct);
    }
}

public sealed record CreateBoardPostRequest(
    string Title,
    string Content,
    BoardPostType PostType = BoardPostType.Issue,
    string? CaptchaToken = null);
public sealed record CreateBoardPostResponse(bool Success, string Message, int? PostId = null);
