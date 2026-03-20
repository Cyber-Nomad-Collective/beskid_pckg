namespace Server.Features.Packages;

public sealed record PackageSummaryResponse(
    Guid Id,
    string Name,
    string Description,
    string? RepositoryUrl,
    string? WebsiteUrl,
    bool IsPublic,
    long TotalDownloads,
    DateTimeOffset UpdatedAtUtc,
    int PendingReviewsCount,
    double AverageRating);

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

public sealed record UpsertPackageRequest(
    string Name,
    string? Description,
    string? RepositoryUrl,
    string? WebsiteUrl,
    bool IsPublic,
    bool SubmitForReview,
    string? ReviewReason);

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
