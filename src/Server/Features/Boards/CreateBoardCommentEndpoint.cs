using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using Server.Data;
using Server.Services;
using Server.Services.Notifications;
using System.Security.Claims;

namespace Server.Features.Boards;

public sealed class CreateBoardCommentEndpoint : Endpoint<CreateBoardCommentRequest, CreateBoardCommentResponse>
{
    public ApplicationDbContext Db { get; set; } = default!;
    public IUserRatingService RatingService { get; set; } = default!;
    public INotificationService Notifications { get; set; } = default!;
    public ICaptchaVerificationService Captcha { get; set; } = default!;
    public ILinkContentGuard LinkGuard { get; set; } = default!;

    public override void Configure()
    {
        Post("/boards/posts/{postId}/comments");
        Roles("User", "SuperAdmin", "Moderator");
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

        var remoteIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        if (!await Captcha.IsHumanAsync(req.CaptchaToken, CaptchaActions.BoardComment, remoteIp, ct))
        {
            await Send.ResponseAsync(new CreateBoardCommentResponse(false, "Robot check failed. Please try again."), StatusCodes.Status400BadRequest, ct);
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
            await Send.ResponseAsync(new CreateBoardCommentResponse(false, "This post is locked."), StatusCodes.Status403Forbidden, ct);
            return;
        }

        var linkBlock = await LinkGuard.GetBlockReasonAsync(req.Content, ct);
        if (linkBlock is not null)
        {
            await Send.ResponseAsync(new CreateBoardCommentResponse(false, linkBlock), StatusCodes.Status400BadRequest, ct);
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

        var participantIds = await Db.BoardPostComments
            .AsNoTracking()
            .Where(c => c.PostId == postId && !c.IsDeleted)
            .Select(c => c.AuthorUserId)
            .Distinct()
            .ToListAsync(ct);

        var targetUserIds = participantIds
            .Append(post.AuthorUserId)
            .Where(id => !string.Equals(id, userId, StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        foreach (var targetUserId in targetUserIds)
        {
            await Notifications.PublishAsync(
                targetUserId,
                NotificationType.BoardThreadActivity,
                $"New reply in: {post.Title}",
                "Someone replied in a thread you participated in.",
                preferenceScope: NotificationPreferenceScope.Thread,
                preferenceScopeId: postId.ToString(),
                ct: ct);
        }

        await Send.OkAsync(new CreateBoardCommentResponse(true, "Comment created successfully.", comment.Id), ct);
    }
}

public sealed record CreateBoardCommentRequest(
    string Content,
    int? ParentCommentId,
    string? CaptchaToken = null);
public sealed record CreateBoardCommentResponse(bool Success, string Message, int? CommentId = null);
