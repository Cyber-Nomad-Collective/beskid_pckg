using System.Text.Json;
using System.Text.Json.Serialization;

namespace Server.Components.Docs;

public sealed class StructuredApiDocDto
{
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; set; }

    [JsonPropertyName("source")]
    public string? Source { get; set; }

    [JsonPropertyName("generator")]
    public string? Generator { get; set; }

    [JsonPropertyName("items")]
    public List<StructuredApiItemDto> Items { get; set; } = [];
}

public sealed class StructuredApiItemDto
{
    public int? Id { get; set; }

    [JsonPropertyName("qualifiedName")]
    public string? QualifiedName { get; set; }

    public string? Name { get; set; }

    public string? Kind { get; set; }

    public string? Visibility { get; set; }

    public StructuredLocationDto? Location { get; set; }

    [JsonPropertyName("doc_markdown")]
    public string? DocMarkdown { get; set; }

    public JsonElement[]? Controls { get; set; }
}

public sealed class StructuredLocationDto
{
    public string? File { get; set; }

    [JsonPropertyName("startLine")]
    public int StartLine { get; set; }

    [JsonPropertyName("startColumn")]
    public int StartColumn { get; set; }

    [JsonPropertyName("endLine")]
    public int EndLine { get; set; }

    [JsonPropertyName("endColumn")]
    public int EndColumn { get; set; }
}

/// <summary>Qualified-name prefix tree for module navigation.</summary>
public sealed class NavTreeNode
{
    public SortedDictionary<string, NavTreeNode> Children { get; } = new(StringComparer.Ordinal);

    public List<StructuredApiItemDto> Members { get; } = [];

    public IEnumerable<string> ChildSegments => Children.Keys;

    public NavTreeNode GetOrAddChild(string segment)
    {
        if (!Children.TryGetValue(segment, out var child))
        {
            child = new NavTreeNode();
            Children[segment] = child;
        }

        return child;
    }
}
