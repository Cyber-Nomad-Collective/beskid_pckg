using System.Net;
using System.Net.Http.Json;
using System.Text;
using Server.Features.Packages;
using Server.Tests.TestUtils;

namespace Server.Tests.Integration;

public class PackageSourceIntegrationTests : IClassFixture<TestApplicationFactory>
{
    private readonly TestApplicationFactory _factory;

    public PackageSourceIntegrationTests(TestApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task SourceTree_Returns_Hierarchy_With_Type_Hints()
    {
        var (_, apiKey, package) = await _factory.SeedOwnerWithPackageAsync("Source.Tree", isPublic: true);
        var extras = new Dictionary<string, string>
        {
            ["src/Main.bd"] = "fn Main() {}\n",
            ["src/System/Config.json"] = "{ \"ok\": true }\n",
            ["assets/logo.png"] = "fakepng",
        };
        var artifact = BpkTestArtifactBuilder.CreateValidArtifact(package.Name, "1.0.0", extras);
        var digest = BpkTestArtifactBuilder.ArtifactSha256(artifact);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-API-Key", apiKey);
        using var publishForm = new MultipartFormDataContent
        {
            { new StringContent("1.0.0"), "version" },
            { new StringContent(digest), "checksumSha256" },
            { new ByteArrayContent(artifact), "artifact", "source-tree.bpk" },
        };
        var publish = await client.PostAsync($"/api/packages/{package.Name}/publish", publishForm);
        Assert.Equal(HttpStatusCode.OK, publish.StatusCode);

        client.DefaultRequestHeaders.Remove("X-API-Key");
        var response = await client.GetAsync($"/api/packages/{package.Name}/versions/1.0.0/source/tree");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<PackageSourceTreeResponse>();
        Assert.NotNull(payload);
        Assert.Contains(payload!.Nodes, n => n.IsDirectory && string.Equals(n.Path, "src", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(payload.Nodes, n => !n.IsDirectory && string.Equals(n.Path, "src/Main.bd", StringComparison.OrdinalIgnoreCase) && n.FileType == "beskid");
        Assert.Contains(payload.Nodes, n => !n.IsDirectory && string.Equals(n.Path, "src/System/Config.json", StringComparison.OrdinalIgnoreCase) && n.FileType == "json");
        Assert.Contains(payload.Nodes, n => !n.IsDirectory && string.Equals(n.Path, "assets/logo.png", StringComparison.OrdinalIgnoreCase) && n.PreviewKind == "image");
    }

    [Fact]
    public async Task SourceFile_Text_Returns_Body_And_Monaco_Header()
    {
        var (_, apiKey, package) = await _factory.SeedOwnerWithPackageAsync("Source.Text", isPublic: true);
        var source = "fn Main() {\n  let x = 1;\n}\n";
        var extras = new Dictionary<string, string> { ["src/Main.bd"] = source };
        var artifact = BpkTestArtifactBuilder.CreateValidArtifact(package.Name, "2.0.0", extras);
        var digest = BpkTestArtifactBuilder.ArtifactSha256(artifact);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-API-Key", apiKey);
        using var publishForm = new MultipartFormDataContent
        {
            { new StringContent("2.0.0"), "version" },
            { new StringContent(digest), "checksumSha256" },
            { new ByteArrayContent(artifact), "artifact", "source-text.bpk" },
        };
        await client.PostAsync($"/api/packages/{package.Name}/publish", publishForm);
        client.DefaultRequestHeaders.Remove("X-API-Key");

        var response = await client.GetAsync(
            $"/api/packages/{package.Name}/versions/2.0.0/source/file?path={Uri.EscapeDataString("src/Main.bd")}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text", response.Headers.GetValues("X-Beskid-Source-Preview").Single());
        Assert.Equal("rust", response.Headers.GetValues("X-Beskid-Monaco-Language").Single());
        var text = await response.Content.ReadAsStringAsync();
        Assert.Contains("fn Main()", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SourceFile_Image_Returns_Binary_Content()
    {
        var (_, apiKey, package) = await _factory.SeedOwnerWithPackageAsync("Source.Image", isPublic: true);
        var pngBytes = Encoding.ASCII.GetBytes("PNGDATA");
        using var ms = new MemoryStream();
        using (var zip = new System.IO.Compression.ZipArchive(ms, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
        {
            var entries = new Dictionary<string, byte[]>
            {
                ["Project.proj"] = Encoding.UTF8.GetBytes($"project {{\n  name = \"{package.Name}\"\n}}\n"),
                ["src/entry.bd"] = Encoding.UTF8.GetBytes("fn Main() {}\n"),
                ["assets/logo.png"] = pngBytes,
                [".beskid/docs/api.json"] = Encoding.UTF8.GetBytes(BpkTestArtifactBuilder.MinimalStructuredApiJson),
                ["package.json"] = Encoding.UTF8.GetBytes($$"""{"schema":"beskid.package.v1","id":"{{package.Name}}","version":"1.0.0"}"""),
            };

            var checksumLines = entries.OrderBy(x => x.Key, StringComparer.Ordinal)
                .Select(e => $"{Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(e.Value)).ToLowerInvariant()}  {e.Key}");
            entries["checksums.sha256"] = Encoding.UTF8.GetBytes(string.Join('\n', checksumLines) + "\n");

            foreach (var kv in entries.OrderBy(x => x.Key, StringComparer.Ordinal))
            {
                var entry = zip.CreateEntry(kv.Key, System.IO.Compression.CompressionLevel.Fastest);
                await using var stream = entry.Open();
                await stream.WriteAsync(kv.Value);
            }
        }
        var artifact = ms.ToArray();
        var digest = BpkTestArtifactBuilder.ArtifactSha256(artifact);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-API-Key", apiKey);
        using var publishForm = new MultipartFormDataContent
        {
            { new StringContent("1.0.0"), "version" },
            { new StringContent(digest), "checksumSha256" },
            { new ByteArrayContent(artifact), "artifact", "source-image.bpk" },
        };
        await client.PostAsync($"/api/packages/{package.Name}/publish", publishForm);
        client.DefaultRequestHeaders.Remove("X-API-Key");

        var response = await client.GetAsync(
            $"/api/packages/{package.Name}/versions/1.0.0/source/file?path={Uri.EscapeDataString("assets/logo.png")}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image", response.Headers.GetValues("X-Beskid-Source-Preview").Single());
        Assert.Equal("image/png", response.Content.Headers.ContentType?.MediaType);
        var content = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal(pngBytes, content);
    }

    [Fact]
    public async Task SourceFile_Unsafe_Path_Returns_BadRequest()
    {
        var (_, apiKey, package) = await _factory.SeedOwnerWithPackageAsync("Source.Unsafe", isPublic: true);
        var artifact = BpkTestArtifactBuilder.CreateValidArtifact(
            package.Name,
            "1.0.0",
            new Dictionary<string, string> { ["src/main.bd"] = "fn Main() {}\n" });
        var digest = BpkTestArtifactBuilder.ArtifactSha256(artifact);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-API-Key", apiKey);
        using var publishForm = new MultipartFormDataContent
        {
            { new StringContent("1.0.0"), "version" },
            { new StringContent(digest), "checksumSha256" },
            { new ByteArrayContent(artifact), "artifact", "source-unsafe.bpk" },
        };
        await client.PostAsync($"/api/packages/{package.Name}/publish", publishForm);
        client.DefaultRequestHeaders.Remove("X-API-Key");

        var bad = await client.GetAsync(
            $"/api/packages/{package.Name}/versions/1.0.0/source/file?path={Uri.EscapeDataString("src/../Project.proj")}");
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
    }
}
