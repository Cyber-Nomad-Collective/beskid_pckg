using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Server.Components.Docs;
using Server.Contracts.ApiDocumentation;
using Server.Features.Packages;
using Server.Services;
using Server.Tests.TestUtils;

namespace Server.Tests.Integration;

public class PackageDocsIntegrationTests : IClassFixture<TestApplicationFactory>
{
    private readonly TestApplicationFactory _factory;

    public PackageDocsIntegrationTests(TestApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task DocsIndex_AfterPublish_Returns_Markdown_Files()
    {
        var (_, apiKey, package) = await _factory.SeedOwnerWithPackageAsync("Docs.Index", isPublic: true);
        var extras = new Dictionary<string, string>
        {
            ["docs/guide.md"] = "# Guide\n\nHello.",
            ["README.md"] = "# Readme\n\nOverview.",
        };
        var artifact = BpkTestArtifactBuilder.CreateValidArtifact(package.Name, "1.0.0", extras);
        var digest = BpkTestArtifactBuilder.ArtifactSha256(artifact);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-API-Key", apiKey);

        using var publishForm = new MultipartFormDataContent
        {
            { new StringContent("1.0.0"), "version" },
            { new StringContent(digest), "checksumSha256" },
            { new ByteArrayContent(artifact), "artifact", "docs.bpk" },
        };

        var publish = await client.PostAsync($"/api/packages/{package.Name}/publish", publishForm);
        Assert.Equal(HttpStatusCode.OK, publish.StatusCode);

        client.DefaultRequestHeaders.Remove("X-API-Key");
        var index = await client.GetAsync($"/api/packages/{package.Name}/versions/1.0.0/docs");
        Assert.Equal(HttpStatusCode.OK, index.StatusCode);
        var payload = await index.Content.ReadFromJsonAsync<PackageDocsIndexResponse>();
        Assert.NotNull(payload);
        var paths = payload!.Files.Select(f => f.Path).ToList();
        Assert.Contains("README.md", paths, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("docs/guide.md", paths, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DocsIndex_Includes_BeskidCliGenerated_Docs_Under_DotBeskid_Docs()
    {
        var (_, apiKey, package) = await _factory.SeedOwnerWithPackageAsync("Docs.BeskidGen", isPublic: true);
        var extras = new Dictionary<string, string>
        {
            [".beskid/docs/index.md"] = "# API\n\nGenerated.",
            [".beskid/docs/api.json"] = "{}",
        };
        var artifact = BpkTestArtifactBuilder.CreateValidArtifact(package.Name, "1.0.0", extras);
        var digest = BpkTestArtifactBuilder.ArtifactSha256(artifact);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-API-Key", apiKey);

        using var publishForm = new MultipartFormDataContent
        {
            { new StringContent("1.0.0"), "version" },
            { new StringContent(digest), "checksumSha256" },
            { new ByteArrayContent(artifact), "artifact", "beskid-docs.bpk" },
        };

        var publish = await client.PostAsync($"/api/packages/{package.Name}/publish", publishForm);
        Assert.Equal(HttpStatusCode.OK, publish.StatusCode);

        client.DefaultRequestHeaders.Remove("X-API-Key");
        var index = await client.GetAsync($"/api/packages/{package.Name}/versions/1.0.0/docs");
        Assert.Equal(HttpStatusCode.OK, index.StatusCode);
        var payload = await index.Content.ReadFromJsonAsync<PackageDocsIndexResponse>();
        Assert.NotNull(payload);
        var paths = payload!.Files.Select(f => f.Path).ToList();
        Assert.Contains(".beskid/docs/index.md", paths, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain(".beskid/docs/api.json", paths, StringComparer.OrdinalIgnoreCase);
        Assert.True(payload.HasStructuredApiDoc);
        Assert.Equal(".beskid/docs/api.json", payload.StructuredDocRelativePath, StringComparer.OrdinalIgnoreCase);

        var structured = await client.GetAsync($"/api/packages/{package.Name}/versions/1.0.0/docs/structured");
        Assert.Equal(HttpStatusCode.OK, structured.StatusCode);
        Assert.Equal("application/json", structured.Content.Headers.ContentType?.MediaType);
        var json = await structured.Content.ReadAsStringAsync();
        Assert.Equal("{}", json.Trim());

        var file = await client.GetAsync(
            $"/api/packages/{package.Name}/versions/1.0.0/docs/file?path={Uri.EscapeDataString(".beskid/docs/index.md")}");
        Assert.Equal(HttpStatusCode.OK, file.StatusCode);
        var text = await file.Content.ReadAsStringAsync();
        Assert.Contains("Generated.", text);
    }

    [Fact]
    public async Task DocsStructured_GraphV3_ApiJson_Deserializes_And_Is_Indexable()
    {
        const string apiJson = """
            {
              "schemaVersion": 3,
              "navigationModel": "graph-v1",
              "source": "fixture.bd",
              "generator": "test",
              "items": [
                {
                  "id": 10,
                  "qualifiedName": "Demo",
                  "name": "Demo",
                  "kind": "module",
                  "visibility": "public",
                  "modulePath": ["Demo"],
                  "parentId": null,
                  "memberIds": [1],
                  "location": { "file": "f.bd", "startLine": 1, "startColumn": 1, "endLine": 1, "endColumn": 1 }
                },
                {
                  "id": 1,
                  "qualifiedName": "Demo::App",
                  "name": "App",
                  "kind": "type",
                  "visibility": "public",
                  "modulePath": ["Demo"],
                  "parentId": 10,
                  "memberIds": [2],
                  "location": { "file": "f.bd", "startLine": 1, "startColumn": 1, "endLine": 1, "endColumn": 1 },
                  "doc": { "summaryMarkdown": "Summary.", "arguments": [], "enumVariants": [], "typeParameters": [] }
                },
                {
                  "id": 2,
                  "qualifiedName": "Demo::App::x",
                  "name": "x",
                  "kind": "field",
                  "visibility": "public",
                  "parentId": 1,
                  "memberIds": [],
                  "location": { "file": "f.bd", "startLine": 2, "startColumn": 1, "endLine": 2, "endColumn": 1 }
                }
              ]
            }
            """;

        var (_, apiKey, package) = await _factory.SeedOwnerWithPackageAsync("Docs.GraphV3", isPublic: true);
        var extras = new Dictionary<string, string>
        {
            [".beskid/docs/api.json"] = apiJson,
            [".beskid/docs/index.md"] = "# API",
        };
        var artifact = BpkTestArtifactBuilder.CreateValidArtifact(package.Name, "1.0.0", extras);
        var digest = BpkTestArtifactBuilder.ArtifactSha256(artifact);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-API-Key", apiKey);

        using var publishForm = new MultipartFormDataContent
        {
            { new StringContent("1.0.0"), "version" },
            { new StringContent(digest), "checksumSha256" },
            { new ByteArrayContent(artifact), "artifact", "graph.bpk" },
        };

        var publish = await client.PostAsync($"/api/packages/{package.Name}/publish", publishForm);
        Assert.Equal(HttpStatusCode.OK, publish.StatusCode);

        client.DefaultRequestHeaders.Remove("X-API-Key");
        var structured = await client.GetAsync($"/api/packages/{package.Name}/versions/1.0.0/docs/structured");
        Assert.Equal(HttpStatusCode.OK, structured.StatusCode);

        var json = await structured.Content.ReadAsStringAsync();
        var doc = JsonSerializer.Deserialize<StructuredApiDocDto>(json, StructuredApiDocJson.Options);
        Assert.NotNull(doc);
        Assert.True(ApiDocNavigationBuilder.SupportsStructuredGraph(doc!));
        var roots = ApiDocNavigationBuilder.BuildLibraryTreeRoots(doc!, package.Name);
        Assert.Single(roots);
        Assert.Equal("Demo", roots[0].Item?.Name);
        var typesFolder = roots[0].Children.FirstOrDefault(c => c.Label == "Types");
        Assert.NotNull(typesFolder);
        Assert.Contains(typesFolder!.Children, c => c.Item?.Id == 1);
        var typeItem = doc!.Items.First(i => i.Id == 1);
        Assert.Equal("Summary.", typeItem.Doc?.SummaryMarkdown);
    }

    [Fact]
    public async Task DocsStructured_ApiJson_Above_MarkdownCap_Below_StructuredCap_Returns_Ok()
    {
        var oversizedJson = "{\"schemaVersion\":3,\"navigationModel\":\"graph-v1\",\"items\":[]"
                            + new string(' ', PackageDocsArchiveService.MaxDocFileBytes + 64_000)
                            + "}";

        var (_, apiKey, package) = await _factory.SeedOwnerWithPackageAsync("Docs.StructuredLarge", isPublic: true);
        var extras = new Dictionary<string, string> { [".beskid/docs/api.json"] = oversizedJson };
        var artifact = BpkTestArtifactBuilder.CreateValidArtifact(package.Name, "1.0.0", extras);
        var digest = BpkTestArtifactBuilder.ArtifactSha256(artifact);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-API-Key", apiKey);

        using var publishForm = new MultipartFormDataContent
        {
            { new StringContent("1.0.0"), "version" },
            { new StringContent(digest), "checksumSha256" },
            { new ByteArrayContent(artifact), "artifact", "large-api.bpk" },
        };

        await client.PostAsync($"/api/packages/{package.Name}/publish", publishForm);
        client.DefaultRequestHeaders.Remove("X-API-Key");

        var structured = await client.GetAsync($"/api/packages/{package.Name}/versions/1.0.0/docs/structured");
        Assert.Equal(HttpStatusCode.OK, structured.StatusCode);
        var json = await structured.Content.ReadAsStringAsync();
        Assert.True(json.Length > PackageDocsArchiveService.MaxDocFileBytes);
    }

    [Fact]
    public async Task Publish_Rejects_Artifact_Without_Structured_Api_Doc()
    {
        var (_, apiKey, package) = await _factory.SeedOwnerWithPackageAsync("Docs.Structured404", isPublic: true);
        var extras = new Dictionary<string, string> { ["docs/only.md"] = "# hi" };
        var artifact = BpkTestArtifactBuilder.CreateValidArtifact(
            package.Name,
            "1.0.0",
            extras,
            includeStructuredApiDoc: false);
        var digest = BpkTestArtifactBuilder.ArtifactSha256(artifact);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-API-Key", apiKey);

        using var publishForm = new MultipartFormDataContent
        {
            { new StringContent("1.0.0"), "version" },
            { new StringContent(digest), "checksumSha256" },
            { new ByteArrayContent(artifact), "artifact", "no-api.bpk" },
        };

        var publish = await client.PostAsync($"/api/packages/{package.Name}/publish", publishForm);
        Assert.Equal(HttpStatusCode.BadRequest, publish.StatusCode);
        var message = await publish.Content.ReadAsStringAsync();
        Assert.Contains("api.json", message, StringComparison.OrdinalIgnoreCase);
        client.DefaultRequestHeaders.Remove("X-API-Key");

        var structured = await client.GetAsync($"/api/packages/{package.Name}/versions/1.0.0/docs/structured");
        Assert.Equal(HttpStatusCode.NotFound, structured.StatusCode);
    }

    [Fact]
    public async Task ReadmeEndpoint_Returns_Root_Readme_From_Artifact()
    {
        var (_, apiKey, package) = await _factory.SeedOwnerWithPackageAsync("Readme.Endpoint", isPublic: true);
        var body = "# Package README\n\nPublished overview.";
        var extras = new Dictionary<string, string> { ["README.md"] = body };
        var artifact = BpkTestArtifactBuilder.CreateValidArtifact(package.Name, "1.0.0", extras);
        var digest = BpkTestArtifactBuilder.ArtifactSha256(artifact);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-API-Key", apiKey);

        using var publishForm = new MultipartFormDataContent
        {
            { new StringContent("1.0.0"), "version" },
            { new StringContent(digest), "checksumSha256" },
            { new ByteArrayContent(artifact), "artifact", "readme.bpk" },
        };

        await client.PostAsync($"/api/packages/{package.Name}/publish", publishForm);
        client.DefaultRequestHeaders.Remove("X-API-Key");

        var readme = await client.GetAsync($"/api/packages/{package.Name}/versions/1.0.0/readme");
        Assert.Equal(HttpStatusCode.OK, readme.StatusCode);
        var text = await readme.Content.ReadAsStringAsync();
        Assert.Contains("Published overview.", text);
    }

    [Fact]
    public async Task DocsFile_Returns_Markdown_Content()
    {
        var (_, apiKey, package) = await _factory.SeedOwnerWithPackageAsync("Docs.File", isPublic: true);
        var body = "# Title\n\n**bold**";
        var extras = new Dictionary<string, string> { ["docs/page.md"] = body };
        var artifact = BpkTestArtifactBuilder.CreateValidArtifact(package.Name, "2.0.0", extras);
        var digest = BpkTestArtifactBuilder.ArtifactSha256(artifact);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-API-Key", apiKey);

        using var publishForm = new MultipartFormDataContent
        {
            { new StringContent("2.0.0"), "version" },
            { new StringContent(digest), "checksumSha256" },
            { new ByteArrayContent(artifact), "artifact", "docs2.bpk" },
        };

        await client.PostAsync($"/api/packages/{package.Name}/publish", publishForm);
        client.DefaultRequestHeaders.Remove("X-API-Key");

        var file = await client.GetAsync(
            $"/api/packages/{package.Name}/versions/latest/docs/file?path={Uri.EscapeDataString("docs/page.md")}");
        Assert.Equal(HttpStatusCode.OK, file.StatusCode);
        var text = await file.Content.ReadAsStringAsync();
        Assert.Contains("**bold**", text);
    }

    [Fact]
    public async Task DocsFile_UnsafePath_Returns_BadRequest()
    {
        var (_, apiKey, package) = await _factory.SeedOwnerWithPackageAsync("Docs.Unsafe", isPublic: true);
        var extras = new Dictionary<string, string> { ["docs/safe.md"] = "# ok" };
        var artifact = BpkTestArtifactBuilder.CreateValidArtifact(package.Name, "1.0.0", extras);
        var digest = BpkTestArtifactBuilder.ArtifactSha256(artifact);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-API-Key", apiKey);

        using var publishForm = new MultipartFormDataContent
        {
            { new StringContent("1.0.0"), "version" },
            { new StringContent(digest), "checksumSha256" },
            { new ByteArrayContent(artifact), "artifact", "unsafe.bpk" },
        };

        await client.PostAsync($"/api/packages/{package.Name}/publish", publishForm);
        client.DefaultRequestHeaders.Remove("X-API-Key");

        var bad = await client.GetAsync(
            $"/api/packages/{package.Name}/versions/1.0.0/docs/file?path={Uri.EscapeDataString("docs/../src/entry.bsk")}");
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
    }

    [Fact]
    public async Task DocsIndex_PrivatePackage_Returns_NotFound_For_Anonymous()
    {
        var (_, apiKey, package) = await _factory.SeedOwnerWithPackageAsync("Docs.Private", isPublic: false);
        var artifact = BpkTestArtifactBuilder.CreateValidArtifact(
            package.Name,
            "1.0.0",
            new Dictionary<string, string> { ["docs/x.md"] = "# x" });
        var digest = BpkTestArtifactBuilder.ArtifactSha256(artifact);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-API-Key", apiKey);

        using var publishForm = new MultipartFormDataContent
        {
            { new StringContent("1.0.0"), "version" },
            { new StringContent(digest), "checksumSha256" },
            { new ByteArrayContent(artifact), "artifact", "priv.bpk" },
        };

        await client.PostAsync($"/api/packages/{package.Name}/publish", publishForm);
        client.DefaultRequestHeaders.Remove("X-API-Key");

        var index = await client.GetAsync($"/api/packages/{package.Name}/versions/1.0.0/docs");
        Assert.Equal(HttpStatusCode.NotFound, index.StatusCode);
    }
}
