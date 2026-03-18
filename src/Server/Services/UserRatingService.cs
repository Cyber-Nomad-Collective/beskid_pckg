using Microsoft.EntityFrameworkCore;
using pckg.Data;

namespace Server.Services;

public sealed class UserRatingService : IUserRatingService
{
    private readonly ApplicationDbContext _db;

    public UserRatingService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<double> GetUserRatingAsync(string userId)
    {
        var rating = await _db.UserRatings.FirstOrDefaultAsync(r => r.UserId == userId);
        return rating?.CalculatedScore ?? 0.0;
    }

    public async Task RecalculateUserRatingAsync(string userId)
    {
        var rating = await _db.UserRatings.FirstOrDefaultAsync(r => r.UserId == userId);
        
        if (rating is null)
        {
            rating = new UserRatingEntity
            {
                UserId = userId,
                ReviewCount = 0,
                BoardPostCount = 0,
                BoardCommentCount = 0,
                HelpfulVoteCount = 0,
                CalculatedScore = 0.0,
                LastCalculatedAtUtc = DateTime.UtcNow
            };
            _db.UserRatings.Add(rating);
        }

        var score = CalculateScore(
            rating.ReviewCount,
            rating.BoardPostCount,
            rating.BoardCommentCount,
            rating.HelpfulVoteCount
        );

        rating.CalculatedScore = score;
        rating.LastCalculatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync();
    }

    public async Task IncrementReviewCountAsync(string userId)
    {
        var rating = await GetOrCreateRatingAsync(userId);
        rating.ReviewCount++;
        await RecalculateUserRatingAsync(userId);
    }

    public async Task IncrementBoardActivityAsync(string userId, bool isPost)
    {
        var rating = await GetOrCreateRatingAsync(userId);
        if (isPost)
            rating.BoardPostCount++;
        else
            rating.BoardCommentCount++;
        await RecalculateUserRatingAsync(userId);
    }

    public async Task IncrementHelpfulVoteAsync(string userId)
    {
        var rating = await GetOrCreateRatingAsync(userId);
        rating.HelpfulVoteCount++;
        await RecalculateUserRatingAsync(userId);
    }

    private async Task<UserRatingEntity> GetOrCreateRatingAsync(string userId)
    {
        var rating = await _db.UserRatings.FirstOrDefaultAsync(r => r.UserId == userId);
        
        if (rating is null)
        {
            rating = new UserRatingEntity
            {
                UserId = userId,
                ReviewCount = 0,
                BoardPostCount = 0,
                BoardCommentCount = 0,
                HelpfulVoteCount = 0,
                CalculatedScore = 0.0,
                LastCalculatedAtUtc = DateTime.UtcNow
            };
            _db.UserRatings.Add(rating);
            await _db.SaveChangesAsync();
        }

        return rating;
    }

    private static double CalculateScore(int reviews, int posts, int comments, int helpfulVotes)
    {
        const double reviewWeight = 5.0;
        const double postWeight = 2.0;
        const double commentWeight = 1.0;
        const double voteWeight = 0.5;

        return (reviews * reviewWeight) + 
               (posts * postWeight) + 
               (comments * commentWeight) + 
               (helpfulVotes * voteWeight);
    }
}
