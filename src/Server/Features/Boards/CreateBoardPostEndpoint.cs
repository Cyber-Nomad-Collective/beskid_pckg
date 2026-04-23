using FastEndpoints;
using Server.Data;
using Server.Services;
using System.Security.Claims;

namespace Server.Features.Boards;

public sealed class CreateBoardPostEndpoint : Endpoint<CreateBoardPostRequest, CreateBoardPostResponse>
{
    public ApplicationDbContext Db { get; set; } = default!;
    public IUserRatingService RatingService { get; set; } = default!;
    public ICaptchaVerificationService Captcha { get; set; } = default!;
    public ILinkContentGuard LinkGuard { get; set; } = default!;

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

        var remoteIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        if (!await Captcha.IsHumanAsync(req.CaptchaToken, CaptchaActions.BoardPost, remoteIp, ct))
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await Send.OkAsync(new CreateBoardPostResponse(false, "Robot check failed. Please try again."), ct);
            return;
        }

        var board = await Db.Boards.FindAsync([boardId], ct);
        if (board is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        if (board.IsLocked)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
            await Send.OkAsync(new CreateBoardPostResponse(false, "This board is locked."), ct);
            return;
        }

        var combined = $"{req.Title}\n{req.Content}";
        var linkBlock = await LinkGuard.GetBlockReasonAsync(combined, ct);
        if (linkBlock is not null)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await Send.OkAsync(new CreateBoardPostResponse(false, linkBlock), ct);
            return;
        }

        var post = new BoardPostEntity
        {
            BoardId = boardId,
            AuthorUserId = userId,
            Title = req.Title,
            Content = req.Content,
            PostType = req.PostType,
            CreatedAtUtc = DateTime.UtcNow,
            UpvoteCount = 0,
            DownvoteCount = 0,
            IsPinned = false,
            IsLocked = false,
            IsDeleted = false
        };

        Db.BoardPosts.Add(post);
        await Db.SaveChangesAsync(ct);

        await RatingService.IncrementBoardActivityAsync(userId, isPost: true);

        await Send.OkAsync(new CreateBoardPostResponse(true, "Post created successfully.", post.Id), ct);
    }
}

public sealed record CreateBoardPostRequest(
    string Title,
    string Content,
    BoardPostType PostType = BoardPostType.Issue,
    string? CaptchaToken = null);
public sealed record CreateBoardPostResponse(bool Success, string Message, int? PostId = null);
