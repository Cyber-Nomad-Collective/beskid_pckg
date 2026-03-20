namespace Server.Data;

public sealed class PackageIssueEntity
{
    public Guid Id { get; set; }
    public Guid PackageId { get; set; }
    public string AuthorUserId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }

    public PackageEntity? Package { get; set; }
}
