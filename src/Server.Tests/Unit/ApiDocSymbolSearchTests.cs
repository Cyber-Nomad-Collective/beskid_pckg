using Server.Components.Docs;
using Server.Contracts.ApiDocumentation;

namespace Server.Tests.Unit;

public class ApiDocSymbolSearchTests
{
    [Fact]
    public void Score_PrefixQualifiedName_RanksAbove_Substring()
    {
        var prefix = new StructuredApiItemDto { QualifiedName = "System.IO.Stream", Name = "Stream" };
        var substring = new StructuredApiItemDto { QualifiedName = "App.MyStreamHelper", Name = "MyStreamHelper" };

        var prefixScore = ApiDocSymbolSearch.Score(prefix, "System.IO");
        var substringScore = ApiDocSymbolSearch.Score(substring, "System.IO");

        Assert.True(prefixScore > substringScore);
    }

    [Fact]
    public void Matches_Includes_Signature_And_Summary()
    {
        var item = new StructuredApiItemDto
        {
            QualifiedName = "Demo.Type",
            Signature = "fn DoWork(): void",
            Doc = new ItemDocStructuredDto { SummaryMarkdown = "Performs work." },
        };

        Assert.True(ApiDocSymbolSearch.Matches(item, "DoWork"));
        Assert.True(ApiDocSymbolSearch.Matches(item, "Performs"));
        Assert.False(ApiDocSymbolSearch.Matches(item, "missing"));
    }
}
