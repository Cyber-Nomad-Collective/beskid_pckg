using Server.Services;

namespace Server.Tests.Unit;

public sealed class DocumentationAnchorRewriterTests
{
    [Fact]
    public void RewriteFragmentOnlyAnchors_Prefixes_Current_Path_For_Double_Quotes()
    {
        var html = """<p><a href="#intro">x</a></p>""";
        var result = DocumentationAnchorRewriter.RewriteFragmentOnlyAnchors(html, "/docs/my-pkg%401.0.0");
        Assert.Equal("""<p><a href="/docs/my-pkg%401.0.0#intro">x</a></p>""", result);
    }

    [Fact]
    public void RewriteFragmentOnlyAnchors_Prefixes_Current_Path_For_Single_Quotes()
    {
        var html = """<a href='#section-2'>y</a>""";
        var result = DocumentationAnchorRewriter.RewriteFragmentOnlyAnchors(html, "/packages/corelib");
        Assert.Equal("""<a href='/packages/corelib#section-2'>y</a>""", result);
    }

    [Fact]
    public void RewriteFragmentOnlyAnchors_Handles_Empty_Fragment()
    {
        var html = """<a href="#">top</a>""";
        var result = DocumentationAnchorRewriter.RewriteFragmentOnlyAnchors(html, "/docs/pkg@latest/api/Foo");
        Assert.Equal("""<a href="/docs/pkg@latest/api/Foo#">top</a>""", result);
    }

    [Fact]
    public void RewriteFragmentOnlyAnchors_Normalizes_Path_Without_Leading_Slash()
    {
        var html = """<a href="#x">z</a>""";
        var result = DocumentationAnchorRewriter.RewriteFragmentOnlyAnchors(html, "packages/foo");
        Assert.Equal("""<a href="/packages/foo#x">z</a>""", result);
    }

    [Fact]
    public void RewriteFragmentOnlyAnchors_Leaves_Other_Hrefs_Unchanged()
    {
        var html = """<a href="/packages/other">p</a> <a href="https://example.com/a#b">e</a>""";
        var result = DocumentationAnchorRewriter.RewriteFragmentOnlyAnchors(html, "/docs/x@y");
        Assert.Equal(html, result);
    }
}
