namespace pckg.Data;

public sealed class PackageReviewEntity
{
    public Guid Id { get; set; }
    public Guid PackageId { get; set; }
    public string RequestedByUserId { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public string? ReviewerUserId { get; set; }
    public string? ReviewNotes { get; set; }
    public DateTimeOffset SubmittedAtUtc { get; set; }
    public DateTimeOffset? ReviewedAtUtc { get; set; }

    public PackageEntity? Package { get; set; }
}
