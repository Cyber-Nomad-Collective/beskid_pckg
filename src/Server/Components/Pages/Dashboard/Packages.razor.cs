using System.Net.Http.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.FluentUI.AspNetCore.Components;
using Server.Components.Shared;
using Server.Features.Packages;

namespace Server.Components.Pages.Dashboard;

public partial class Packages
{
    private const long MaxUploadBytes = 64 * 1024 * 1024;
    private readonly List<PackageSummaryResponse> PackageItems = [];
    private bool IsLoading = true;
    private bool IsSavingMetadata;
    private string? FeedbackMessage;
    private string? MetadataFeedbackMessage;
    private string? EditingPackageName;
    private string? UploadFeedbackMessage;
    private bool UploadFeedbackIsError;
    private string? DeleteFeedbackMessage;
    private bool DeleteFeedbackIsError;
    private readonly MetadataFormModel MetadataForm = new();
    [Inject]
    public IDialogService DialogService { get; set; } = default!;

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

            var items = await response.Content.ReadFromJsonAsync<List<PackageSummaryResponse>>() ?? [];
            PackageItems.Clear();
            PackageItems.AddRange(items.OrderByDescending(x => x.UpdatedAtUtc));
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task PromptDeletePackageAsync(PackageSummaryResponse package)
    {
        DeleteFeedbackMessage = null;
        var content = new DeletePackageConfirmDialog.DeletePackageConfirmContent { PackageName = package.Name };
        var parameters = new DialogParameters
        {
            Width = "min(520px, calc(100vw - 32px))",
            Modal = true,
            TrapFocus = true,
            PreventDismissOnOverlayClick = true
        };

        var dialog = await DialogService.ShowDialogAsync<DeletePackageConfirmDialog>(content, parameters);
        var result = await dialog.Result;
        if (result?.Cancelled != false || result.Data is not bool confirmed || !confirmed)
        {
            return;
        }

        await DeletePackageAsync(package.Name);
    }

    private async Task DeletePackageAsync(string packageName)
    {
        DeleteFeedbackMessage = null;
        DeleteFeedbackIsError = false;

        try
        {
            var response = await Http.DeleteAsync($"/api/packages/{Uri.EscapeDataString(packageName)}");
            var payload = await response.Content.ReadFromJsonAsync<DeletePackageResponse>();
            if (!response.IsSuccessStatusCode || payload is null || !payload.Success)
            {
                DeleteFeedbackIsError = true;
                DeleteFeedbackMessage = payload?.Message ?? "Failed to delete package.";
                return;
            }

            DeleteFeedbackMessage = payload.Message;
            if (EditingPackageName == packageName)
            {
                EditingPackageName = null;
            }

            await LoadPackagesAsync();
        }
        catch
        {
            DeleteFeedbackIsError = true;
            DeleteFeedbackMessage = "Failed to delete package.";
        }
    }

    private void StartMetadataEdit(PackageSummaryResponse package)
    {
        EditingPackageName = package.Name;
        MetadataFeedbackMessage = null;

        MetadataForm.PackageName = package.Name;
        MetadataForm.Description = package.Description;
        MetadataForm.Topic = package.Category;
        MetadataForm.TagsInput = string.Join(", ", package.Tags);
        MetadataForm.RepositoryUrl = package.RepositoryUrl ?? string.Empty;
        MetadataForm.WebsiteUrl = package.WebsiteUrl ?? string.Empty;
        MetadataForm.IconUrl = package.IconUrl ?? string.Empty;
        MetadataForm.IsPublic = package.IsPublic;
    }

    private void CancelMetadataEdit()
    {
        EditingPackageName = null;
        IsSavingMetadata = false;
    }

    private Task StartVersionUpload(PackageSummaryResponse package)
    {
        return OpenUploadDialogAsync(package.Name);
    }

    private Task OpenUploadDialogAsync() => OpenUploadDialogAsync(string.Empty);

    private async Task OpenUploadDialogAsync(string packageName)
    {
        UploadFeedbackMessage = null;
        UploadFeedbackIsError = false;

        var content = new PackageVersionUploadDialog.UploadVersionInput
        {
            PackageName = packageName,
            IsPackageLocked = !string.IsNullOrWhiteSpace(packageName),
            Version = string.Empty,
            ChecksumSha256 = string.Empty
        };

        var parameters = new DialogParameters
        {
            Width = "min(620px, calc(100vw - 32px))",
            Modal = true,
            TrapFocus = true,
            PreventDismissOnOverlayClick = true
        };

        var dialog = await DialogService.ShowDialogAsync<PackageVersionUploadDialog>(content, parameters);
        var result = await dialog.Result;

        if (result?.Cancelled != false || result.Data is not PackageVersionUploadDialog.UploadVersionInput input)
        {
            return;
        }

        await UploadVersionAsync(input);
    }

    private async Task UploadVersionAsync(PackageVersionUploadDialog.UploadVersionInput input)
    {
        if (string.IsNullOrWhiteSpace(input.PackageName))
        {
            UploadFeedbackIsError = true;
            UploadFeedbackMessage = "Package name is required.";
            return;
        }

        if (string.IsNullOrWhiteSpace(input.Version) || input.Artifact is null)
        {
            UploadFeedbackIsError = true;
            UploadFeedbackMessage = "Version and artifact are required.";
            return;
        }

        UploadFeedbackMessage = null;

        try
        {
            await using var fileStream = input.Artifact.OpenReadStream(MaxUploadBytes);
            using var content = new MultipartFormDataContent();
            content.Add(new StringContent(input.Version.Trim()), "version");

            var checksum = input.ChecksumSha256.Trim();
            if (!string.IsNullOrWhiteSpace(checksum))
            {
                content.Add(new StringContent(checksum), "checksumSha256");
            }

            var artifactContent = new StreamContent(fileStream);
            artifactContent.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse("application/zip");
            content.Add(artifactContent, "artifact", input.Artifact.Name);

            var response = await Http.PostAsync($"/api/packages/{Uri.EscapeDataString(input.PackageName)}/publish", content);
            var payload = await response.Content.ReadFromJsonAsync<PublishPackageVersionResponse>();

            if (!response.IsSuccessStatusCode || payload is null || !payload.Success)
            {
                UploadFeedbackIsError = true;
                UploadFeedbackMessage = payload?.Message ?? "Failed to upload package version.";
                return;
            }

            UploadFeedbackIsError = false;
            UploadFeedbackMessage = payload.Message;
            await LoadPackagesAsync();
        }
        catch (IOException)
        {
            UploadFeedbackIsError = true;
            UploadFeedbackMessage = "The selected file exceeds the 64 MB upload limit.";
        }
    }

    private async Task SaveMetadataAsync()
    {
        if (string.IsNullOrWhiteSpace(MetadataForm.PackageName))
        {
            return;
        }

        IsSavingMetadata = true;
        MetadataFeedbackMessage = null;

        try
        {
            var request = new UpsertPackageRequest(
                MetadataForm.PackageName,
                MetadataForm.Description,
                MetadataForm.Topic,
                NormalizeOptionalText(MetadataForm.RepositoryUrl),
                NormalizeOptionalText(MetadataForm.WebsiteUrl),
                ParseTags(MetadataForm.TagsInput),
                MetadataForm.IsPublic,
                false,
                null,
                NormalizeOptionalText(MetadataForm.IconUrl));

            var response = await Http.PostAsJsonAsync("/api/packages", request);
            var payload = await response.Content.ReadFromJsonAsync<UpsertPackageResponse>();
            if (!response.IsSuccessStatusCode || payload is null || !payload.Success || payload.Package is null)
            {
                MetadataFeedbackMessage = payload?.Message ?? "Failed to save package metadata.";
                return;
            }

            var packageIndex = PackageItems.FindIndex(p => p.Name == MetadataForm.PackageName);
            if (packageIndex >= 0)
            {
                PackageItems[packageIndex] = payload.Package;
            }

            MetadataFeedbackMessage = payload.Message;
            EditingPackageName = null;
        }
        finally
        {
            IsSavingMetadata = false;
        }
    }

    private static string? NormalizeOptionalText(string value)
    {
        var trimmed = value.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }

    private static IReadOnlyList<string> ParseTags(string value)
        => value
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private sealed class MetadataFormModel
    {
        public string PackageName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Topic { get; set; } = string.Empty;
        public string TagsInput { get; set; } = string.Empty;
        public string RepositoryUrl { get; set; } = string.Empty;
        public string WebsiteUrl { get; set; } = string.Empty;
        public string IconUrl { get; set; } = string.Empty;
        public bool IsPublic { get; set; }
    }
}