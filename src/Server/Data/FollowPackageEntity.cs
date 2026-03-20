namespace Server.Data;

public sealed class FollowPackageEntity
{
    public Guid Id { get; set; }
    public required string UserId { get; set; }
    public required string PackageId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}
