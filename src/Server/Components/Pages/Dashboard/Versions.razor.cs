namespace Server.Components.Pages.Dashboard;

public partial class Versions
{
    private readonly List<VersionRow> VersionItems = [];
    private bool IsLoading = true;
    private string? FeedbackMessage;

    protected override async Task OnInitializedAsync()
    {
        await LoadVersionsAsync();
    }

    private async Task LoadVersionsAsync()
    {
        IsLoading = true;
        FeedbackMessage = null;
        VersionItems.Clear();

        try
        {
            var packagesResponse = await Http.GetAsync("/api/packages");
            if (!packagesResponse.IsSuccessStatusCode)
            {
                FeedbackMessage = "Unable to load packages.";
                return;
            }

            var packages = await packagesResponse.Content.ReadFromJsonAsync<List<PackageSummaryRow>>() ?? [];
            foreach (var package in packages)
            {
                var versionsResponse =
                    await Http.GetAsync($"/api/packages/{Uri.EscapeDataString(package.Name)}/versions");
                if (!versionsResponse.IsSuccessStatusCode)
                {
                    continue;
                }

                var versions = await versionsResponse.Content.ReadFromJsonAsync<List<VersionRow>>() ?? [];
                VersionItems.AddRange(versions.Where(v =>
                    string.Equals(v.PackageName, package.Name, StringComparison.Ordinal)));
            }

            VersionItems.Sort(static (a, b) => b.PublishedAtUtc.CompareTo(a.PublishedAtUtc));
        }
        finally
        {
            IsLoading = false;
        }
    }

    private sealed record PackageSummaryRow(
        Guid Id,
        string Name,
        string Description,
        string? RepositoryUrl,
        string? WebsiteUrl,
        bool IsPublic,
        DateTimeOffset UpdatedAtUtc,
        int PendingReviewsCount,
        double AverageRating);

    private sealed record VersionRow(
        Guid Id,
        Guid PackageId,
        string PackageName,
        string Version,
        bool IsYanked,
        string ChecksumSha256,
        long SizeBytes,
        DateTimeOffset PublishedAtUtc,
        DateTimeOffset? YankedAtUtc);
}