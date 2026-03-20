using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using Server.Services;
using System.Security.Claims;
using Server.Data;

namespace Server.Features.Boards;

public sealed class VoteBoardPostEndpoint : Endpoint<VoteBoardPostRequest, VoteBoardPostResponse>
{
    public ApplicationDbContext Db { get; set; } = default!;
    public IUserRatingService RatingService { get; set; } = default!;

    public override void Configure()
    {
        Post("/boards/posts/{postId}/vote");
        Roles("User", "SuperAdmin");
    }

    public override async Task HandleAsync(VoteBoardPostRequest req, CancellationToken ct)
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

        var existingVote = await Db.BoardPostVotes
            .FirstOrDefaultAsync(v => v.PostId == postId && v.UserId == userId, ct);

        if (existingVote is not null)
        {
            var oldValue = existingVote.VoteValue;

            // If same vote value, remove the vote (toggle behavior like Reddit)
            if (oldValue == req.VoteValue)
            {
                Db.BoardPostVotes.Remove(existingVote);
                if (oldValue == 1) post.UpvoteCount--;
                else if (oldValue == -1) post.DownvoteCount--;
            }
            else
            {
                existingVote.VoteValue = req.VoteValue;
                if (oldValue == 1) post.UpvoteCount--;
                else if (oldValue == -1) post.DownvoteCount--;
                if (req.VoteValue == 1) post.UpvoteCount++;
                else if (req.VoteValue == -1) post.DownvoteCount++;
            }
        }
        else
        {
            Db.BoardPostVotes.Add(new BoardPostVoteEntity
            {
                PostId = postId,
                UserId = userId,
                VoteValue = req.VoteValue,
                CreatedAtUtc = DateTime.UtcNow
            });

            if (req.VoteValue == 1) post.UpvoteCount++;
            else if (req.VoteValue == -1) post.DownvoteCount++;
        }

        await Db.SaveChangesAsync(ct);

        if (req.VoteValue == 1)
        {
            await RatingService.IncrementHelpfulVoteAsync(post.AuthorUserId);
        }

        await Send.OkAsync(new VoteBoardPostResponse(true, "Vote recorded.", post.UpvoteCount, post.DownvoteCount), ct);
    }
}

public sealed record VoteBoardPostRequest(int VoteValue);
public sealed record VoteBoardPostResponse(bool Success, string Message, int UpvoteCount, int DownvoteCount);
