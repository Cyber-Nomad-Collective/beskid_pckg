namespace Server.Data;

public sealed class FollowPublisherEntity
{
    public Guid Id { get; set; }
    public required string UserId { get; set; }
    public required string PublisherUserId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}
