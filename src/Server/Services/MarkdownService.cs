using Markdig;
using Markdig.Extensions.AutoIdentifiers;

namespace Server.Services;

public interface IMarkdownService
{
    string ToHtml(string markdown);
    string ToSafeHtml(string markdown);
}

public sealed class MarkdownService : IMarkdownService
{
    private readonly MarkdownPipeline _pipeline;
    private readonly MarkdownPipeline _safePipeline;

    public MarkdownService()
    {
        _pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .UseAutoIdentifiers(AutoIdentifierOptions.GitHub)
            .Build();

        _safePipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .UseAutoIdentifiers(AutoIdentifierOptions.GitHub)
            .DisableHtml()
            .Build();
    }

    public string ToHtml(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
            return string.Empty;

        return Markdown.ToHtml(markdown, _pipeline);
    }

    public string ToSafeHtml(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
            return string.Empty;

        return Markdown.ToHtml(markdown, _safePipeline);
    }
}

