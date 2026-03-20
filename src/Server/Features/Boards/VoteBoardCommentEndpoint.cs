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

        if (existingVote is not null)
        {
            var oldValue = existingVote.VoteValue;
            
            // If same vote value, remove the vote (toggle behavior)
            if (oldValue == req.VoteValue)
            {
                Db.BoardCommentVotes.Remove(existingVote);
                if (oldValue == 1) comment.UpvoteCount--;
                else if (oldValue == -1) comment.DownvoteCount--;
            }
            else
            {
                existingVote.VoteValue = req.VoteValue;
                if (oldValue == 1) comment.UpvoteCount--;
                else if (oldValue == -1) comment.DownvoteCount--;
                if (req.VoteValue == 1) comment.UpvoteCount++;
                else if (req.VoteValue == -1) comment.DownvoteCount++;
            }
        }
        else
        {
            Db.BoardCommentVotes.Add(new BoardCommentVoteEntity
            {
                CommentId = commentId,
                UserId = userId,
                VoteValue = req.VoteValue,
                CreatedAtUtc = DateTime.UtcNow
            });

            if (req.VoteValue == 1) comment.UpvoteCount++;
            else if (req.VoteValue == -1) comment.DownvoteCount++;
        }

        await Db.SaveChangesAsync(ct);

        if (req.VoteValue == 1)
        {
            await RatingService.IncrementHelpfulVoteAsync(comment.AuthorUserId);
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

