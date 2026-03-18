using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using pckg.Data;
using pckg.Features.Packages;

namespace Server.Components.Pages;

public partial class PackageDetails
{
    [Parameter] public string PackageName { get; set; } = string.Empty;
    private PackageEntity? Package;
    private string ActiveTab = "versions";
    private readonly List<PackageCommunityReviewEntity> Reviews = [];
    private readonly List<IssueRow> Issues = [];
    private readonly ReviewInput ReviewForm = new();
    private readonly IssueInput IssueForm = new();
    private readonly List<PackageVersionSummaryResponse> Versions = [];
    private bool IsAuthenticated => HttpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;
    private bool CanManageVersions => IsAuthenticated && (HttpContextAccessor.HttpContext?.User?.IsInRole("SuperAdmin") == true || Package?.OwnerUserId == HttpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier));

    protected override async Task OnParametersSetAsync()
    {
        Package = await DbContext.Packages.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Name == PackageName && x.IsPublic);
        await LoadSecondaryDataAsync();
    }

    private async Task LoadSecondaryDataAsync()
    {
        Reviews.Clear();
        Issues.Clear();
        Versions.Clear();

        if (Package is null)
        {
            return;
        }

        Reviews.AddRange(await DbContext.PackageCommunityReviews
            .AsNoTracking()
            .Where(x => x.PackageId == Package.Id)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(30)
            .ToListAsync());

        var issueRows = await DbContext.PackageIssues
            .AsNoTracking()
            .Where(x => x.PackageId == Package.Id)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(50)
            .ToListAsync();

        var issueIds = issueRows.Select(x => x.Id).ToList();
        var scores = await DbContext.PackageIssueVotes
            .AsNoTracking()
            .Where(x => issueIds.Contains(x.IssueId))
            .GroupBy(x => x.IssueId)
            .Select(g => new { g.Key, Score = g.Sum(v => v.Value) })
            .ToDictionaryAsync(x => x.Key, x => x.Score);

        Issues.AddRange(issueRows.Select(x => new IssueRow(x.Id, x.Title, x.Body, scores.GetValueOrDefault(x.Id))));

        var versionRows = await DbContext.PackageVersions
            .AsNoTracking()
            .Where(x => x.PackageId == Package.Id)
            .OrderByDescending(x => x.PublishedAtUtc)
            .ToListAsync();

        Versions.AddRange(versionRows.Select(x => new PackageVersionSummaryResponse(
            x.Id,
            x.PackageId,
            Package.Name,
            x.Version,
            x.IsYanked,
            x.ChecksumSha256,
            x.SizeBytes,
            x.PublishedAtUtc,
            x.YankedAtUtc)));
    }

    private async Task AddReviewAsync()
    {
        var userId = HttpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (Package is null || string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(ReviewForm.Comment))
        {
            return;
        }

        DbContext.PackageCommunityReviews.Add(new PackageCommunityReviewEntity
        {
            Id = Guid.NewGuid(),
            PackageId = Package.Id,
            UserId = userId,
            Rating = Math.Clamp(ReviewForm.Rating, 1, 5),
            Comment = ReviewForm.Comment.Trim(),
            CreatedAtUtc = DateTimeOffset.UtcNow,
        });
        await DbContext.SaveChangesAsync();

        ReviewForm.Rating = 5;
        ReviewForm.Comment = string.Empty;
        await LoadSecondaryDataAsync();
    }

    private async Task AddIssueAsync()
    {
        var userId = HttpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (Package is null || string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(IssueForm.Title) ||
            string.IsNullOrWhiteSpace(IssueForm.Body))
        {
            return;
        }

        DbContext.PackageIssues.Add(new PackageIssueEntity
        {
            Id = Guid.NewGuid(),
            PackageId = Package.Id,
            AuthorUserId = userId,
            Title = IssueForm.Title.Trim(),
            Body = IssueForm.Body.Trim(),
            CreatedAtUtc = DateTimeOffset.UtcNow,
        });
        await DbContext.SaveChangesAsync();

        IssueForm.Title = string.Empty;
        IssueForm.Body = string.Empty;
        await LoadSecondaryDataAsync();
    }

    private async Task VoteAsync(Guid issueId, int value)
    {
        var userId = HttpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId) || value is not (1 or -1))
        {
            return;
        }

        var existing =
            await DbContext.PackageIssueVotes.SingleOrDefaultAsync(x => x.IssueId == issueId && x.UserId == userId);
        if (existing is null)
        {
            DbContext.PackageIssueVotes.Add(new PackageIssueVoteEntity
            {
                Id = Guid.NewGuid(),
                IssueId = issueId,
                UserId = userId,
                Value = value,
                CreatedAtUtc = DateTimeOffset.UtcNow,
            });
        }
        else
        {
            existing.Value = value;
        }

        await DbContext.SaveChangesAsync();
        await LoadSecondaryDataAsync();
    }

    private async Task DeleteVersionAsync(PackageVersionSummaryResponse version)
    {
        if (!CanManageVersions)
        {
            return;
        }

        var entity = await DbContext.PackageVersions.FindAsync(version.Id);
        if (entity is not null)
        {
            DbContext.PackageVersions.Remove(entity);
            await DbContext.SaveChangesAsync();
            await LoadSecondaryDataAsync();
        }
    }

    private static string FormatSize(long bytes)
    {
        string[] suf = { "B", "KB", "MB", "GB", "TB" };
        if (bytes == 0) return "0 B";
        var bytesDouble = (double)bytes;
        var place = Convert.ToInt32(Math.Floor(Math.Log(bytesDouble, 1024)));
        var num = Math.Round(bytesDouble / Math.Pow(1024, place), 1);
        return $"{num} {suf[place]}";
    }

    private sealed class ReviewInput
    {
        public int Rating { get; set; } = 5;
        public string Comment { get; set; } = string.Empty;
    }

    private sealed class IssueInput
    {
        public string Title { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
    }

    private sealed record IssueRow(Guid Id, string Title, string Body, int Score);
}