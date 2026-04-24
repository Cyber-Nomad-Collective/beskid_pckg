using System.Text.Json.Serialization;

namespace Server.Features.Packages;

public sealed record PackageSummaryResponse(
    Guid Id,
    string Name,
    string Description,
    string Category,
    string? RepositoryUrl,
    string? WebsiteUrl,
    IReadOnlyList<string> Tags,
    bool IsPublic,
    long TotalDownloads,
    DateTimeOffset UpdatedAtUtc,
    int PendingReviewsCount,
    double AverageRating,
    [property: JsonPropertyName("iconUrl")] string? IconUrl);

public sealed record PackageVersionSummaryResponse(
    Guid Id,
    Guid PackageId,
    string PackageName,
    string Version,
    bool IsYanked,
    string ChecksumSha256,
    long SizeBytes,
    DateTimeOffset PublishedAtUtc,
    DateTimeOffset? YankedAtUtc);

public sealed record PublishPackageVersionResponse(
    bool Success,
    string Message,
    PackageVersionSummaryResponse? Version);

public sealed record PackageVersionLifecycleResponse(
    bool Success,
    string Message,
    PackageVersionSummaryResponse? Version);

public sealed record UpsertPackageRequest(
    string Name,
    string? Description,
    string? Category,
    string? RepositoryUrl,
    string? WebsiteUrl,
    IReadOnlyList<string>? Tags,
    bool IsPublic,
    bool SubmitForReview,
    string? ReviewReason,
    [property: JsonPropertyName("iconUrl")] string? IconUrl);

public sealed record UpsertPackageResponse(
    bool Success,
    string Message,
    PackageSummaryResponse? Package,
    Guid? ReviewId);

public sealed record PackageReviewResponse(
    Guid Id,
    Guid PackageId,
    string PackageName,
    string RequestedByUserId,
    string Reason,
    string Status,
    DateTimeOffset SubmittedAtUtc,
    string? ReviewerUserId,
    string? ReviewNotes,
    DateTimeOffset? ReviewedAtUtc);

public sealed record ReviewActionRequest(
    Guid ReviewId,
    string Action,
    string? Notes);

public sealed record ReviewActionResponse(
    bool Success,
    string Message,
    PackageReviewResponse? Review);

public sealed record CommunityReviewRequest(Guid PackageId, int Rating, string Comment);

public sealed record CommunityReviewResponse(
    Guid Id,
    Guid PackageId,
    string UserId,
    int Rating,
    string Comment,
    DateTimeOffset CreatedAtUtc);

public sealed record PackageIssueRequest(Guid PackageId, string Title, string Body);

public sealed record PackageIssueResponse(
    Guid Id,
    Guid PackageId,
    string Title,
    string Body,
    string AuthorUserId,
    DateTimeOffset CreatedAtUtc,
    int Score);

public sealed record VoteIssueRequest(Guid IssueId, int Value);

public sealed record PackageDependencyResponse(
    string Name,
    string? Version,
    string Source,
    string? Registry);

public sealed record PackageHealthSnapshotResponse(
    string State,
    string SubState,
    double Score,
    string UpdateRateState,
    string UpdateRateSubState,
    double UpdateRateNormalized,
    double UpdateRateWeight,
    string DownloadsState,
    string DownloadsSubState,
    double DownloadsNormalized,
    double DownloadsWeight,
    string ReviewsState,
    string ReviewsSubState,
    double ReviewsNormalized,
    double ReviewsWeight);

public sealed record PackageSearchResponse(
    PackageSummaryResponse Package,
    int ReviewCount,
    PackageHealthSnapshotResponse Health);

public sealed record PackageDetailsResponse(
    PackageSummaryResponse Package,
    IReadOnlyList<PackageVersionSummaryResponse> Versions,
    IReadOnlyList<PackageDependencyResponse> Dependencies,
    int DependentsCount,
    string? Readme,
    PackageHealthSnapshotResponse Health,
    [property: JsonPropertyName("firstPublishedAtUtc")] DateTimeOffset? FirstPublishedAtUtc = null,
    [property: JsonPropertyName("lastPublishedAtUtc")] DateTimeOffset? LastPublishedAtUtc = null,
    [property: JsonPropertyName("latestVersion")] string? LatestVersion = null);

public sealed record PackageDocFileEntry(
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("title")] string Title);

public sealed record PackageDocsIndexResponse(
    [property: JsonPropertyName("files")] IReadOnlyList<PackageDocFileEntry> Files,
    [property: JsonPropertyName("hasStructuredApiDoc")] bool HasStructuredApiDoc = false,
    [property: JsonPropertyName("structuredDocRelativePath")] string? StructuredDocRelativePath = null);

public sealed record PackageSourceTreeNodeResponse(
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("isDirectory")] bool IsDirectory,
    [property: JsonPropertyName("parentPath")] string? ParentPath,
    [property: JsonPropertyName("sizeBytes")] long? SizeBytes,
    [property: JsonPropertyName("fileType")] string FileType,
    [property: JsonPropertyName("iconKey")] string IconKey,
    [property: JsonPropertyName("previewKind")] string PreviewKind,
    [property: JsonPropertyName("monacoLanguage")] string? MonacoLanguage,
    [property: JsonPropertyName("contentType")] string? ContentType);

public sealed record PackageSourceTreeResponse(
    [property: JsonPropertyName("nodes")] IReadOnlyList<PackageSourceTreeNodeResponse> Nodes);
