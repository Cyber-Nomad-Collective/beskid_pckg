namespace Server.Data;

public sealed class PackageTagEntity
{
    public int Id { get; set; }
    public Guid PackageId { get; set; }
    public required string Tag { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}
