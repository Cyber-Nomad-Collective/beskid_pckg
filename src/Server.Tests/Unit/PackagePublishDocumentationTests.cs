using Server.Services;

namespace Server.Tests.Unit;

public class PackagePublishDocumentationTests
{
    [Fact]
    public void EnsureStructuredApiDoc_passes_when_api_json_present()
    {
        var entries = new Dictionary<string, byte[]>
        {
            [PackageDocsPaths.StructuredApiDocRelativePath] = "{}"u8.ToArray(),
            ["src/Main.bd"] = "//"u8.ToArray(),
        };

        var ex = Record.Exception(() => PackagePublishDocumentation.EnsureStructuredApiDoc(entries, "demo"));
        Assert.Null(ex);
    }

    [Fact]
    public void EnsureStructuredApiDoc_throws_when_api_json_missing()
    {
        var entries = new Dictionary<string, byte[]>
        {
            ["src/Main.bd"] = "//"u8.ToArray(),
        };

        var ex = Assert.Throws<InvalidOperationException>(() =>
            PackagePublishDocumentation.EnsureStructuredApiDoc(entries, "demo"));

        Assert.Contains("api.json", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("demo", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EnsureStructuredApiDoc_skips_template_packages()
    {
        var entries = new Dictionary<string, byte[]>
        {
            [PackageTemplatePaths.TemplateJsonRelativePath] = "{}"u8.ToArray(),
            ["src/Main.bd"] = "//"u8.ToArray(),
        };
        const string packageJson = """{"schema":"beskid.package.v1","id":"tpl","version":"1.0.0","packageKind":"template"}""";

        var ex = Record.Exception(() =>
            PackagePublishDocumentation.EnsureStructuredApiDoc(entries, "tpl", packageJson));

        Assert.Null(ex);
    }

    [Fact]
    public void EnsureStructuredApiDoc_skips_tool_packages()
    {
        var entries = new Dictionary<string, byte[]>
        {
            ["src/Main.bd"] = "//"u8.ToArray(),
        };
        const string packageJson = """{"schema":"beskid.package.v1","id":"beskid.tools.demo","version":"1.0.0","packageKind":"tool"}""";

        var ex = Record.Exception(() =>
            PackagePublishDocumentation.EnsureStructuredApiDoc(entries, "beskid.tools.demo", packageJson));

        Assert.Null(ex);
    }
}
