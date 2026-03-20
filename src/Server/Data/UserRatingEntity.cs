namespace Server.Data;

public sealed class UserRatingEntity
{
    public int Id { get; set; }
    public required string UserId { get; set; }
    public int KarmaPoints { get; set; }
    public int ReviewCount { get; set; }
    public int BoardPostCount { get; set; }
    public int BoardCommentCount { get; set; }
    public int HelpfulVoteCount { get; set; }
    public double CalculatedScore { get; set; }
    public DateTime LastCalculatedAtUtc { get; set; }
}
