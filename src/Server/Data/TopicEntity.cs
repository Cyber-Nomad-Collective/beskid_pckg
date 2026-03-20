namespace Server.Data;

public sealed class TopicEntity
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Slug { get; set; }
    public string Description { get; set; } = string.Empty;
    public required string CreatedByUserId { get; set; }
    public int BoardId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
