namespace Server.Data;

public sealed class BoardEntity
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Slug { get; set; }
    public string Description { get; set; } = string.Empty;
    public required string EntityType { get; set; }
    public string? EntityId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public bool IsLocked { get; set; }
}
