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
}
