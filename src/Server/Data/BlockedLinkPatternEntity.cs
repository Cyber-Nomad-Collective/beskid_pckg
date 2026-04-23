namespace Server.Data;

public sealed class BlockedLinkPatternEntity
{
    public Guid Id { get; set; }
    public required string Pattern { get; set; }
    public string? Note { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}
