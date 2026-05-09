using Server.Services;

namespace Server.Tests.Unit;

public sealed class DocumentationAnchorRewriterTests
{
    [Fact]
    public void RewriteDocumentationAnchors_Prefixes_Current_Path_For_Double_Quoted_Fragments()
    {
        var html = """<p><a href="#intro">x</a></p>""";
        var result = DocumentationAnchorRewriter.RewriteDocumentationAnchors(html, "/docs/my-pkg%401.0.0");
        Assert.Equal("""<p><a href="/docs/my-pkg%401.0.0#intro">x</a></p>""", result);
    }

    [Fact]
    public void RewriteDocumentationAnchors_Prefixes_Current_Path_For_Single_Quoted_Fragments()
    {
        var html = """<a href='#section-2'>y</a>""";
        var result = DocumentationAnchorRewriter.RewriteDocumentationAnchors(html, "/packages/corelib");
        Assert.Equal("""<a href='/packages/corelib#section-2'>y</a>""", result);
    }

    [Fact]
    public void RewriteDocumentationAnchors_Handles_Empty_Fragment()
    {
        var html = """<a href="#">top</a>""";
        var result = DocumentationAnchorRewriter.RewriteDocumentationAnchors(html, "/docs/pkg@latest/api/Foo");
        Assert.Equal("""<a href="/docs/pkg@latest/api/Foo#">top</a>""", result);
    }

    [Fact]
    public void RewriteDocumentationAnchors_Normalizes_Path_Without_Leading_Slash()
    {
        var html = """<a href="#x">z</a>""";
        var result = DocumentationAnchorRewriter.RewriteDocumentationAnchors(html, "packages/foo");
        Assert.Equal("""<a href="/packages/foo#x">z</a>""", result);
    }

    [Fact]
    public void RewriteDocumentationAnchors_Leaves_Site_Root_And_External_Hrefs_Unchanged()
    {
        var html = """<a href="/packages/other">p</a> <a href="https://example.com/a#b">e</a>""";
        var result = DocumentationAnchorRewriter.RewriteDocumentationAnchors(html, "/docs/x@y");
        Assert.Equal(html, result);
    }

    [Fact]
    public void RewriteDocumentationAnchors_Rewrites_Empty_Href_To_Current_Path()
    {
        var html = """<a href="">self</a>""";
        var result = DocumentationAnchorRewriter.RewriteDocumentationAnchors(html, "/docs/corelib@1.0.0/api/Mod::Type");
        Assert.Equal("""<a href="/docs/corelib@1.0.0/api/Mod::Type">self</a>""", result);
    }

    [Fact]
    public void RewriteDocumentationAnchors_Resolves_Dot_Slash_Relative_To_Directory()
    {
        var html = """<a href="./Other.md">t</a>""";
        var result = DocumentationAnchorRewriter.RewriteDocumentationAnchors(html, "/docs/p@v/api/Mod::Type");
        Assert.Equal("""<a href="/docs/p@v/api/Other.md">t</a>""", result);
    }

    [Fact]
    public void RewriteDocumentationAnchors_Resolves_Relative_File_To_Directory()
    {
        var html = """<a href="Sibling">t</a>""";
        var result = DocumentationAnchorRewriter.RewriteDocumentationAnchors(html, "/docs/p@v/api/Mod::Type");
        Assert.Equal("""<a href="/docs/p@v/api/Sibling">t</a>""", result);
    }

    [Fact]
    public void RewriteDocumentationAnchors_Resolves_Relative_With_Fragment()
    {
        var html = """<a href="./x#y">t</a>""";
        var result = DocumentationAnchorRewriter.RewriteDocumentationAnchors(html, "/docs/p@v/api/Q");
        Assert.Equal("""<a href="/docs/p@v/api/x#y">t</a>""", result);
    }
}
