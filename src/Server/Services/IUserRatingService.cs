namespace Server.Services;

public interface IUserRatingService
{
    Task<double> GetUserRatingAsync(string userId);
    Task RecalculateUserRatingAsync(string userId);
    Task IncrementReviewCountAsync(string userId);
    Task IncrementBoardActivityAsync(string userId, bool isPost);
    Task IncrementHelpfulVoteAsync(string userId);
}
