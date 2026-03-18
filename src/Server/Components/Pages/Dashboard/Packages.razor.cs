namespace Server.Components.Pages.Dashboard;

public partial class Packages
{
    private readonly List<PackageSummaryRow> PackageItems = [];
    private bool IsLoading = true;
    private string? FeedbackMessage;

    protected override async Task OnInitializedAsync()
    {
        await LoadPackagesAsync();
    }

    private async Task LoadPackagesAsync()
    {
        IsLoading = true;
        FeedbackMessage = null;

        try
        {
            var response = await Http.GetAsync("/api/packages");
            if (!response.IsSuccessStatusCode)
            {
                FeedbackMessage = "Unable to load packages.";
                return;
            }

            var items = await response.Content.ReadFromJsonAsync<List<PackageSummaryRow>>() ?? [];
            PackageItems.Clear();
            PackageItems.AddRange(items.OrderByDescending(x => x.UpdatedAtUtc));
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
}