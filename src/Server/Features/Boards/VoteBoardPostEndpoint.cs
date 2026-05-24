using System.Security.Claims;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using Server.Data;
using Server.Services;

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
        var normalizedVote = req.VoteValue is 1 or -1 ? req.VoteValue : 0;

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

        var previousVote = 0;
        var newVote = normalizedVote;

        if (existingVote is not null)
        {
            var oldValue = existingVote.VoteValue;
            previousVote = oldValue;

            // If same vote value, remove the vote (toggle behavior like Reddit)
            if (oldValue == normalizedVote)
            {
                Db.BoardPostVotes.Remove(existingVote);
                if (oldValue == 1) post.UpvoteCount--;
                else if (oldValue == -1) post.DownvoteCount--;
                newVote = 0;
            }
            else
            {
                existingVote.VoteValue = normalizedVote;
                if (oldValue == 1) post.UpvoteCount--;
                else if (oldValue == -1) post.DownvoteCount--;
                if (normalizedVote == 1) post.UpvoteCount++;
                else if (normalizedVote == -1) post.DownvoteCount++;
            }
        }
        else
        {
            Db.BoardPostVotes.Add(new BoardPostVoteEntity
            {
                PostId = postId,
                UserId = userId,
                VoteValue = normalizedVote,
                CreatedAtUtc = DateTime.UtcNow
            });

            if (normalizedVote == 1) post.UpvoteCount++;
            else if (normalizedVote == -1) post.DownvoteCount++;
        }

        await Db.SaveChangesAsync(ct);

        var karmaDelta = newVote - previousVote;
        if (karmaDelta != 0 && !string.Equals(post.AuthorUserId, userId, StringComparison.Ordinal))
        {
            await RatingService.AdjustKarmaAsync(post.AuthorUserId, karmaDelta);
            if (karmaDelta > 0)
            {
                await RatingService.IncrementHelpfulVoteAsync(post.AuthorUserId);
            }
        }

        await Send.OkAsync(new VoteBoardPostResponse(true, "Vote recorded.", post.UpvoteCount, post.DownvoteCount), ct);
    }
}

public sealed record VoteBoardPostRequest(int VoteValue);
public sealed record VoteBoardPostResponse(bool Success, string Message, int UpvoteCount, int DownvoteCount);
