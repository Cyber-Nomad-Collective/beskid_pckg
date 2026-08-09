using Server.Data;
using Server.Services;
using Server.Services.Notifications;
using Microsoft.EntityFrameworkCore;

namespace Server.Features.Boards;

/// <summary>Canonical mutation rules shared by the legacy and React board contracts.</summary>
public sealed class BoardMutationService(
    ApplicationDbContext db,
    ICaptchaVerificationService captcha,
    ILinkContentGuard linkGuard,
    IUserRatingService ratings,
    INotificationService notifications)
{
    public async Task<BoardMutationResult<BoardPostEntity>> CreatePostAsync(
        int boardId, string userId, string title, string content, BoardPostType postType, string? captchaToken, string? remoteIp, CancellationToken ct)
    {
        if (!await captcha.IsHumanAsync(captchaToken, CaptchaActions.BoardPost, remoteIp, ct))
            return BoardMutationResult<BoardPostEntity>.BadRequest("Robot check failed. Please try again.");
        var board = await db.Boards.FindAsync([boardId], ct);
        if (board is null) return BoardMutationResult<BoardPostEntity>.NotFound();
        if (board.IsLocked) return BoardMutationResult<BoardPostEntity>.Forbidden("This board is locked.");
        var blocked = await linkGuard.GetBlockReasonAsync($"{title}\n{content}", ct);
        if (blocked is not null) return BoardMutationResult<BoardPostEntity>.BadRequest(blocked);
        var post = new BoardPostEntity { BoardId = boardId, AuthorUserId = userId, Title = title, Content = content, PostType = postType, CreatedAtUtc = DateTime.UtcNow };
        db.BoardPosts.Add(post); await db.SaveChangesAsync(ct); await ratings.IncrementBoardActivityAsync(userId, true);
        return BoardMutationResult<BoardPostEntity>.Ok(post);
    }

    public async Task<BoardMutationResult<BoardPostCommentEntity>> CreateCommentAsync(int postId, string userId, string content, int? parentCommentId, string? captchaToken, string? remoteIp, CancellationToken ct)
    {
        if (!await captcha.IsHumanAsync(captchaToken, CaptchaActions.BoardComment, remoteIp, ct)) return BoardMutationResult<BoardPostCommentEntity>.BadRequest("Robot check failed. Please try again.");
        var post = await db.BoardPosts.FindAsync([postId], ct);
        if (post is null || post.IsDeleted) return BoardMutationResult<BoardPostCommentEntity>.NotFound();
        if (post.IsLocked) return BoardMutationResult<BoardPostCommentEntity>.Forbidden("This post is locked.");
        var blocked = await linkGuard.GetBlockReasonAsync(content, ct);
        if (blocked is not null) return BoardMutationResult<BoardPostCommentEntity>.BadRequest(blocked);
        var comment = new BoardPostCommentEntity { PostId = postId, ParentCommentId = parentCommentId, AuthorUserId = userId, Content = content, CreatedAtUtc = DateTime.UtcNow };
        db.BoardPostComments.Add(comment); await db.SaveChangesAsync(ct); await ratings.IncrementBoardActivityAsync(userId, false);
        var participants = await db.BoardPostComments.AsNoTracking().Where(item => item.PostId == postId && !item.IsDeleted).Select(item => item.AuthorUserId).Distinct().ToListAsync(ct);
        foreach (var recipient in participants.Append(post.AuthorUserId).Where(recipient => !string.Equals(recipient, userId, StringComparison.Ordinal)).Distinct())
            await notifications.PublishAsync(recipient, NotificationType.BoardThreadActivity, $"New reply in: {post.Title}", "Someone replied in a thread you participated in.", preferenceScope: NotificationPreferenceScope.Thread, preferenceScopeId: postId.ToString(), ct: ct);
        return BoardMutationResult<BoardPostCommentEntity>.Ok(comment);
    }

    public async Task<BoardMutationResult<BoardVoteCounts>> VotePostAsync(int postId, string userId, int value, CancellationToken ct)
    {
        var post = await db.BoardPosts.FindAsync([postId], ct); if (post is null || post.IsDeleted) return BoardMutationResult<BoardVoteCounts>.NotFound();
        value = value is 1 or -1 ? value : 0; var existing = await db.BoardPostVotes.FirstOrDefaultAsync(v => v.PostId == postId && v.UserId == userId, ct); var previous = existing?.VoteValue ?? 0; var current = value;
        if (existing is not null && previous == value) { db.BoardPostVotes.Remove(existing); current = 0; } else if (existing is not null) existing.VoteValue = value; else db.BoardPostVotes.Add(new BoardPostVoteEntity { PostId = postId, UserId = userId, VoteValue = value, CreatedAtUtc = DateTime.UtcNow });
        post.UpvoteCount += (current == 1 ? 1 : 0) - (previous == 1 ? 1 : 0); post.DownvoteCount += (current == -1 ? 1 : 0) - (previous == -1 ? 1 : 0); await db.SaveChangesAsync(ct);
        if (current != previous && !string.Equals(post.AuthorUserId, userId, StringComparison.Ordinal)) { await ratings.AdjustKarmaAsync(post.AuthorUserId, current - previous); if (current > previous) await ratings.IncrementHelpfulVoteAsync(post.AuthorUserId); }
        return BoardMutationResult<BoardVoteCounts>.Ok(new(post.UpvoteCount, post.DownvoteCount));
    }

    public async Task<BoardMutationResult<BoardVoteCounts>> VoteCommentAsync(int commentId, string userId, int value, CancellationToken ct)
    {
        var comment = await db.BoardPostComments.FindAsync([commentId], ct); if (comment is null || comment.IsDeleted) return BoardMutationResult<BoardVoteCounts>.NotFound();
        value = value is 1 or -1 ? value : 0; var existing = await db.BoardCommentVotes.FirstOrDefaultAsync(v => v.CommentId == commentId && v.UserId == userId, ct); var previous = existing?.VoteValue ?? 0; var current = value;
        if (existing is not null && previous == value) { db.BoardCommentVotes.Remove(existing); current = 0; } else if (existing is not null) existing.VoteValue = value; else db.BoardCommentVotes.Add(new BoardCommentVoteEntity { CommentId = commentId, UserId = userId, VoteValue = value, CreatedAtUtc = DateTime.UtcNow });
        comment.UpvoteCount += (current == 1 ? 1 : 0) - (previous == 1 ? 1 : 0); comment.DownvoteCount += (current == -1 ? 1 : 0) - (previous == -1 ? 1 : 0); await db.SaveChangesAsync(ct);
        if (current != previous && !string.Equals(comment.AuthorUserId, userId, StringComparison.Ordinal)) { await ratings.AdjustKarmaAsync(comment.AuthorUserId, current - previous); if (current > previous) await ratings.IncrementHelpfulVoteAsync(comment.AuthorUserId); }
        return BoardMutationResult<BoardVoteCounts>.Ok(new(comment.UpvoteCount, comment.DownvoteCount));
    }

    public static async Task<BoardMutationResult<BoardEntity>> SetBoardLockedAsync(ApplicationDbContext db, int boardId, string userId, bool locked, IAuthorizationService authorization, CancellationToken ct)
    {
        var board = await db.Boards.FindAsync([boardId], ct);
        if (board is null) return BoardMutationResult<BoardEntity>.NotFound();
        if (!await authorization.CanModerateBoardAsync(userId, boardId)) return BoardMutationResult<BoardEntity>.Forbidden("You cannot change lock state for this board.");
        board.IsLocked = locked; await db.SaveChangesAsync(ct);
        return BoardMutationResult<BoardEntity>.Ok(board);
    }
}

public sealed record BoardVoteCounts(int Upvotes, int Downvotes);

public sealed record BoardMutationResult<T>(int StatusCode, string? Message, T? Value)
{
    public static BoardMutationResult<T> Ok(T value) => new(StatusCodes.Status200OK, null, value);
    public static BoardMutationResult<T> NotFound() => new(StatusCodes.Status404NotFound, null, default);
    public static BoardMutationResult<T> Forbidden(string message) => new(StatusCodes.Status403Forbidden, message, default);
    public static BoardMutationResult<T> BadRequest(string message) => new(StatusCodes.Status400BadRequest, message, default);
}
