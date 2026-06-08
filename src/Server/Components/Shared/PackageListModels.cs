namespace Server.Components.Shared;

public sealed record PackageListRow(
    string Name,
    string Description,
    string? IconUrl,
    string Category,
    long TotalDownloads,
    double AverageRating,
    int ReviewCount,
    DateTimeOffset UpdatedAtUtc);

public sealed record PackageCarouselSlide(
    string Id,
    string Title,
    string Description,
    IReadOnlyList<PackageListRow> Packages);

public sealed record HomeCommunityPostRow(
    int PostId,
    string Title,
    string BoardName,
    string? PackageName,
    string AuthorDisplayName,
    DateTime CreatedAtUtc,
    int NetVotes);

public sealed record HomeReviewRow(
    Guid ReviewId,
    string PackageName,
    string? PackageIconUrl,
    string AuthorDisplayName,
    int Rating,
    string CommentPreview,
    DateTimeOffset CreatedAtUtc);

public sealed record HomeCategoryRow(string Category, int Count, string TopPackageName);
