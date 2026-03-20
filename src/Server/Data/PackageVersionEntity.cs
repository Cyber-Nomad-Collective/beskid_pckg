namespace Server.Data;

public sealed class PackageVersionEntity
{
    public Guid Id { get; set; }
    public Guid PackageId { get; set; }
    public string Version { get; set; } = string.Empty;
    public string ManifestJson { get; set; } = string.Empty;
    public string ChecksumSha256 { get; set; } = string.Empty;
    public string StorageKey { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/zip";
    public long SizeBytes { get; set; }
    public bool IsYanked { get; set; }
    public DateTimeOffset PublishedAtUtc { get; set; }
    public DateTimeOffset? YankedAtUtc { get; set; }

    public PackageEntity? Package { get; set; }
}
