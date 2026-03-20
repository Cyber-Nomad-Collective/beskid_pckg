namespace Server.Data;

public sealed class NotificationEntity
{
    public Guid Id { get; set; }
    public required string UserId { get; set; }
    public NotificationType Type { get; set; }
    public required string Title { get; set; }
    public string? Message { get; set; }
    public string? DataJson { get; set; }
    public bool IsRead { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}
