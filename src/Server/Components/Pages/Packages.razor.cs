using Microsoft.EntityFrameworkCore;
using pckg.Features.Packages;

namespace Server.Components.Pages;

public partial class Packages
{
    private readonly List<PackageSummaryResponse> Rows = [];
    private string Search = string.Empty;
    protected override async Task OnInitializedAsync() => await LoadAsync();

    private async Task LoadAsync()
    {
        var query = DbContext.Packages.AsNoTracking().Where(x => x.IsPublic);
        if (!string.IsNullOrWhiteSpace(Search))
        {
            var needle = Search.Trim();
            query = query.Where(x =>
                x.Name.Contains(needle) || x.Category.Contains(needle) || x.Description.Contains(needle));
        }

        Rows.Clear();
        var packageIds = await query.Select(x => x.Id).ToListAsync();
        
        var pendingCounts = await DbContext.PackageReviews
            .AsNoTracking()
            .Where(x => packageIds.Contains(x.PackageId) && x.Status == "Pending")
            .GroupBy(x => x.PackageId)
            .Select(group => new { group.Key, Count = group.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);
            
        var ratingAverages = await DbContext.PackageCommunityReviews
            .AsNoTracking()
            .Where(x => packageIds.Contains(x.PackageId))
            .GroupBy(x => x.PackageId)
            .Select(group => new { group.Key, Average = group.Average(x => x.Rating) })
            .ToDictionaryAsync(x => x.Key, x => x.Average);

        var packageRows = await query
            .Select(x => new PackageSummaryResponse(
                x.Id,
                x.Name,
                x.Description,
                x.RepositoryUrl,
                x.WebsiteUrl,
                x.IsPublic,
                x.UpdatedAtUtc,
                0,
                0.0))
            .ToListAsync();
            
        packageRows = packageRows.Select(x => x with {
            PendingReviewsCount = pendingCounts.GetValueOrDefault(x.Id),
            AverageRating = Math.Round(ratingAverages.GetValueOrDefault(x.Id), 2)
        }).ToList();

        Rows.AddRange(packageRows
            .OrderByDescending(x => x.PendingReviewsCount)
            .ThenByDescending(x => x.UpdatedAtUtc)
            .Take(100));
    }

    private async Task ResetAsync()
    {
        Search = string.Empty;
        await LoadAsync();
    }
}