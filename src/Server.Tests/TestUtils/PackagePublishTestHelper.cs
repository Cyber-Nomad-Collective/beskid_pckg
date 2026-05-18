using System.Net.Http.Headers;
using System.Net.Http.Json;
using Server.Features.Packages;

namespace Server.Tests.TestUtils;

public static class PackagePublishTestHelper
{
    public static async Task<HttpResponseMessage> PublishVersionAsync(
        HttpClient client,
        string packageName,
        string version,
        string checksumSha256,
        byte[] artifactZipBytes,
        string apiKey)
    {
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(version), "version");
        content.Add(new StringContent(checksumSha256), "checksumSha256");

        var artifact = new ByteArrayContent(artifactZipBytes);
        artifact.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
        content.Add(artifact, "artifact", "package.bpk");

        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/packages/{Uri.EscapeDataString(packageName)}/publish")
        {
            Content = content,
        };
        request.Headers.Add("X-API-Key", apiKey);

        return await client.SendAsync(request);
    }

    public static async Task<PublishPackageVersionResponse?> PublishVersionOrNullAsync(
        HttpClient client,
        string packageName,
        string version,
        string checksumSha256,
        byte[] artifactZipBytes,
        string apiKey)
    {
        var response = await PublishVersionAsync(client, packageName, version, checksumSha256, artifactZipBytes, apiKey);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<PublishPackageVersionResponse>();
    }
}
