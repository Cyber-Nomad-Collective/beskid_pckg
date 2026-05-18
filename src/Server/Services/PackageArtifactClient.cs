using System.Net.Http.Json;
using System.Text.Json;
using Server.Contracts.ApiDocumentation;
using Server.Features.Packages;

namespace Server.Services;

public interface IPackageArtifactClient
{
    Task<ArtifactLoadResult<PackageDocsIndexResponse>> GetDocsIndexAsync(
        string packageIdentifier,
        string version,
        CancellationToken cancellationToken = default);

    Task<ArtifactLoadResult<StructuredApiDocDto>> GetStructuredDocAsync(
        string packageIdentifier,
        string version,
        CancellationToken cancellationToken = default);

    Task<ArtifactLoadResult<PackageSourceTreeResponse>> GetSourceTreeAsync(
        string packageIdentifier,
        string version,
        CancellationToken cancellationToken = default);
}

public sealed record ArtifactLoadResult<T>(bool Success, T? Value, string? ErrorMessage, int? StatusCode)
{
    public static ArtifactLoadResult<T> Ok(T value) => new(true, value, null, StatusCodes.Status200OK);
    public static ArtifactLoadResult<T> Fail(string message, int? statusCode = null)
        => new(false, default, message, statusCode);
}

public sealed class PackageArtifactClient(HttpClient http) : IPackageArtifactClient
{
    public async Task<ArtifactLoadResult<PackageDocsIndexResponse>> GetDocsIndexAsync(
        string packageIdentifier,
        string version,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(packageIdentifier) || string.IsNullOrWhiteSpace(version))
        {
            return ArtifactLoadResult<PackageDocsIndexResponse>.Fail("Package or version is missing.");
        }

        try
        {
            var url = PackageDocumentationUrls.DocsIndex(packageIdentifier.Trim(), version.Trim());
            var response = await http.GetAsync(url, cancellationToken);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return ArtifactLoadResult<PackageDocsIndexResponse>.Fail(
                    "Documentation was not found for this package version.",
                    (int)response.StatusCode);
            }

            if (!response.IsSuccessStatusCode)
            {
                return ArtifactLoadResult<PackageDocsIndexResponse>.Fail(
                    "Could not load documentation.",
                    (int)response.StatusCode);
            }

            var payload = await response.Content.ReadFromJsonAsync<PackageDocsIndexResponse>(cancellationToken: cancellationToken);
            return ArtifactLoadResult<PackageDocsIndexResponse>.Ok(payload ?? new PackageDocsIndexResponse([]));
        }
        catch
        {
            return ArtifactLoadResult<PackageDocsIndexResponse>.Fail("Could not load documentation (network or unexpected error).");
        }
    }

    public async Task<ArtifactLoadResult<StructuredApiDocDto>> GetStructuredDocAsync(
        string packageIdentifier,
        string version,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(packageIdentifier) || string.IsNullOrWhiteSpace(version))
        {
            return ArtifactLoadResult<StructuredApiDocDto>.Fail("Package or version is missing.");
        }

        try
        {
            var url = PackageDocumentationUrls.DocsStructured(packageIdentifier.Trim(), version.Trim());
            var response = await http.GetAsync(url, cancellationToken);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return ArtifactLoadResult<StructuredApiDocDto>.Fail(
                    "Structured API documentation was not found for this package version.",
                    (int)response.StatusCode);
            }

            if (!response.IsSuccessStatusCode)
            {
                return ArtifactLoadResult<StructuredApiDocDto>.Fail(
                    "Could not load structured API documentation.",
                    (int)response.StatusCode);
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(json))
            {
                return ArtifactLoadResult<StructuredApiDocDto>.Fail("Structured API documentation was empty.");
            }

            var doc = JsonSerializer.Deserialize<StructuredApiDocDto>(json, StructuredApiDocJson.Options);
            if (doc is null)
            {
                return ArtifactLoadResult<StructuredApiDocDto>.Fail("Structured API documentation could not be parsed.");
            }

            return ArtifactLoadResult<StructuredApiDocDto>.Ok(doc);
        }
        catch
        {
            return ArtifactLoadResult<StructuredApiDocDto>.Fail("Could not load structured API documentation.");
        }
    }

    public async Task<ArtifactLoadResult<PackageSourceTreeResponse>> GetSourceTreeAsync(
        string packageIdentifier,
        string version,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(packageIdentifier) || string.IsNullOrWhiteSpace(version))
        {
            return ArtifactLoadResult<PackageSourceTreeResponse>.Fail("Package or version is missing.");
        }

        try
        {
            var response = await http.GetAsync(
                PackageDocumentationUrls.SourceTree(packageIdentifier.Trim(), version.Trim()),
                cancellationToken);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return ArtifactLoadResult<PackageSourceTreeResponse>.Fail(
                    "Source tree was not found for this package version.",
                    (int)response.StatusCode);
            }

            if (!response.IsSuccessStatusCode)
            {
                return ArtifactLoadResult<PackageSourceTreeResponse>.Fail(
                    "Could not load source tree.",
                    (int)response.StatusCode);
            }

            var payload = await response.Content.ReadFromJsonAsync<PackageSourceTreeResponse>(cancellationToken: cancellationToken);
            if (payload?.Nodes is not { Count: > 0 })
            {
                return ArtifactLoadResult<PackageSourceTreeResponse>.Fail("No source files were found in this package version.");
            }

            return ArtifactLoadResult<PackageSourceTreeResponse>.Ok(payload);
        }
        catch
        {
            return ArtifactLoadResult<PackageSourceTreeResponse>.Fail("Could not load source tree (network or unexpected error).");
        }
    }
}
