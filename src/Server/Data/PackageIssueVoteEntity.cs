namespace pckg.Data;

public sealed class PackageIssueVoteEntity
{
    public Guid Id { get; set; }
    public Guid IssueId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public int Value { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }

    public PackageIssueEntity? Issue { get; set; }
}
