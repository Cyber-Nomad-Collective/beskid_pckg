using FastEndpoints;
using Server.Services;
using System.Security.Claims;
using Server.Data;

namespace Server.Features.Boards;

public sealed class CreateBoardCommentEndpoint : Endpoint<CreateBoardCommentRequest, CreateBoardCommentResponse>
{
    public ApplicationDbContext Db { get; set; } = default!;
    public IUserRatingService RatingService { get; set; } = default!;

    public override void Configure()
    {
        Post("/boards/posts/{postId}/comments");
        Roles("User", "SuperAdmin");
    }

    public override async Task HandleAsync(CreateBoardCommentRequest req, CancellationToken ct)
    {
        var postId = Route<int>("postId");
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        var post = await Db.BoardPosts.FindAsync([postId], ct);
        if (post is null || post.IsDeleted)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        if (post.IsLocked)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
            await Send.OkAsync(new CreateBoardCommentResponse(false, "This post is locked."), ct);
            return;
        }

        var comment = new BoardPostCommentEntity
        {
            PostId = postId,
            ParentCommentId = req.ParentCommentId,
            AuthorUserId = userId,
            Content = req.Content,
            CreatedAtUtc = DateTime.UtcNow,
            UpvoteCount = 0,
            DownvoteCount = 0,
            IsDeleted = false
        };

        Db.BoardPostComments.Add(comment);
        await Db.SaveChangesAsync(ct);

        await RatingService.IncrementBoardActivityAsync(userId, isPost: false);

        await Send.OkAsync(new CreateBoardCommentResponse(true, "Comment created successfully.", comment.Id), ct);
    }
}

public sealed record CreateBoardCommentRequest(string Content, int? ParentCommentId);
public sealed record CreateBoardCommentResponse(bool Success, string Message, int? CommentId = null);
