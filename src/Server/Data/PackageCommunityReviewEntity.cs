namespace pckg.Data;

public sealed class PackageCommunityReviewEntity
{
    public Guid Id { get; set; }
    public Guid PackageId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public int Rating { get; set; }
    public string Comment { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }

    public PackageEntity? Package { get; set; }
}
