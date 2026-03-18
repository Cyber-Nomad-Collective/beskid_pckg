using Microsoft.EntityFrameworkCore;

namespace Server.Components.Pages;

public partial class Packages
{
    private readonly List<PackageRow> Rows = [];
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
        var packageRows = await query
            .Select(x => new PackageRow(x.Name, x.Category, x.TotalDownloads, x.UpdatedAtUtc))
            .ToListAsync();

        Rows.AddRange(packageRows
            .OrderByDescending(x => x.TotalDownloads)
            .ThenByDescending(x => x.UpdatedAtUtc)
            .Take(100));
    }

    private async Task ResetAsync()
    {
        Search = string.Empty;
        await LoadAsync();
    }

    private static string GetCategoryLabel(PackageRow row)
    {
        return string.IsNullOrWhiteSpace(row.Category) ? "General" : row.Category.Trim();
    }

    private static string FormatDownloads(long downloads)
    {
        if (downloads >= 1_000_000)
        {
            return $"{downloads / 1_000_000d:0.#}M";
        }

        if (downloads >= 1_000)
        {
            return $"{downloads / 1_000d:0.#}K";
        }

        return downloads.ToString();
    }

    private sealed record PackageRow(string Name, string Category, long TotalDownloads, DateTimeOffset UpdatedAtUtc);
}