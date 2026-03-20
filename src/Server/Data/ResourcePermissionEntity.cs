namespace Server.Data;

public sealed class ResourcePermissionEntity
{
    public int Id { get; set; }
    public required string UserId { get; set; }
    public required string ResourceType { get; set; }
    public required string ResourceId { get; set; }
    public required string Permission { get; set; }
    public required string GrantedByUserId { get; set; }
    public DateTime GrantedAtUtc { get; set; }
}
