using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using Server.Services;
using System.Security.Claims;
using Server.Data;

namespace Server.Features.Boards;

public sealed class VoteBoardCommentEndpoint : Endpoint<VoteBoardCommentRequest, VoteBoardCommentResponse>
{
    public ApplicationDbContext Db { get; set; } = default!;
    public IUserRatingService RatingService { get; set; } = default!;

    public override void Configure()
    {
        Post("/boards/comments/{commentId}/vote");
        Roles("User", "SuperAdmin");
    }

    public override async Task HandleAsync(VoteBoardCommentRequest req, CancellationToken ct)
    {
        var commentId = Route<int>("commentId");
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var normalizedVote = req.VoteValue is 1 or -1 ? req.VoteValue : 0;

        if (string.IsNullOrWhiteSpace(userId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        var comment = await Db.BoardPostComments.FindAsync([commentId], ct);
        if (comment is null || comment.IsDeleted)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var existingVote = await Db.BoardCommentVotes
            .FirstOrDefaultAsync(v => v.CommentId == commentId && v.UserId == userId, ct);

        var previousVote = 0;
        var newVote = normalizedVote;

        if (existingVote is not null)
        {
            var oldValue = existingVote.VoteValue;
            previousVote = oldValue;
            
            // If same vote value, remove the vote (toggle behavior)
            if (oldValue == normalizedVote)
            {
                Db.BoardCommentVotes.Remove(existingVote);
                if (oldValue == 1) comment.UpvoteCount--;
                else if (oldValue == -1) comment.DownvoteCount--;
                newVote = 0;
            }
            else
            {
                existingVote.VoteValue = normalizedVote;
                if (oldValue == 1) comment.UpvoteCount--;
                else if (oldValue == -1) comment.DownvoteCount--;
                if (normalizedVote == 1) comment.UpvoteCount++;
                else if (normalizedVote == -1) comment.DownvoteCount++;
            }
        }
        else
        {
            Db.BoardCommentVotes.Add(new BoardCommentVoteEntity
            {
                CommentId = commentId,
                UserId = userId,
                VoteValue = normalizedVote,
                CreatedAtUtc = DateTime.UtcNow
            });

            if (normalizedVote == 1) comment.UpvoteCount++;
            else if (normalizedVote == -1) comment.DownvoteCount++;
        }

        await Db.SaveChangesAsync(ct);

        var karmaDelta = newVote - previousVote;
        if (karmaDelta != 0 && !string.Equals(comment.AuthorUserId, userId, StringComparison.Ordinal))
        {
            await RatingService.AdjustKarmaAsync(comment.AuthorUserId, karmaDelta);
            if (karmaDelta > 0)
            {
                await RatingService.IncrementHelpfulVoteAsync(comment.AuthorUserId);
            }
        }

        await Send.OkAsync(new VoteBoardCommentResponse(
            true, 
            "Vote recorded.", 
            comment.UpvoteCount, 
            comment.DownvoteCount
        ), ct);
    }
}

public sealed record VoteBoardCommentRequest(int VoteValue);
public sealed record VoteBoardCommentResponse(bool Success, string Message, int UpvoteCount, int DownvoteCount);

