namespace Server.Services;

public interface IDocumentationRendering
{
    string RenderPackageMarkdown(string markdown, string currentDocPath);
}

public sealed class DocumentationRendering(
    IMarkdownService markdownService,
    IHtmlSanitizationService htmlSanitization) : IDocumentationRendering
{
    public string RenderPackageMarkdown(string markdown, string currentDocPath)
    {
        var safe = markdownService.ToSafeHtml(markdown);
        var sanitized = htmlSanitization.Sanitize(safe);
        return DocumentationAnchorRewriter.RewriteDocumentationAnchors(sanitized, currentDocPath);
    }
}
