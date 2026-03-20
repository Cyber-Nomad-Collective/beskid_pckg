namespace Server.Data;

public sealed class PackageEntity
{
    public Guid Id { get; set; }
    public string OwnerUserId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = "General";
    public string Description { get; set; } = string.Empty;
    public string? IconUrl { get; set; }
    public string? RepositoryUrl { get; set; }
    public string? WebsiteUrl { get; set; }
    public bool IsPublic { get; set; } = true;
    public long TotalDownloads { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
