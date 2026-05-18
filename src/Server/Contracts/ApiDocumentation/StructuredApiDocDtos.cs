using System.Text.Json;
using System.Text.Json.Serialization;

namespace Server.Contracts.ApiDocumentation;

public sealed class StructuredApiDocDto
{
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; set; }

    /// <summary>When <c>graph-v1</c>, navigation must use <see cref="StructuredApiItemDto.ParentId"/> / <see cref="StructuredApiItemDto.MemberIds"/> only.</summary>
    [JsonPropertyName("navigationModel")]
    public string? NavigationModel { get; set; }

    [JsonPropertyName("source")]
    public string? Source { get; set; }

    [JsonPropertyName("generator")]
    public string? Generator { get; set; }

    [JsonPropertyName("items")]
    public List<StructuredApiItemDto> Items { get; set; } = [];
}

public sealed class ItemDocArgumentDto
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("markdown")]
    public string Markdown { get; set; } = string.Empty;
}

public sealed class ItemDocStructuredDto
{
    [JsonPropertyName("summaryMarkdown")]
    public string? SummaryMarkdown { get; set; }

    [JsonPropertyName("returnsMarkdown")]
    public string? ReturnsMarkdown { get; set; }

    [JsonPropertyName("arguments")]
    public List<ItemDocArgumentDto> Arguments { get; set; } = [];

    [JsonPropertyName("enumVariants")]
    public List<ItemDocArgumentDto> EnumVariants { get; set; } = [];

    [JsonPropertyName("typeParameters")]
    public List<ItemDocArgumentDto> TypeParameters { get; set; } = [];
}

public sealed class StructuredApiItemDto
{
    public int? Id { get; set; }

    [JsonPropertyName("qualifiedName")]
    public string? QualifiedName { get; set; }

    public string? Name { get; set; }

    public string? Kind { get; set; }

    public string? Visibility { get; set; }

    [JsonPropertyName("parentId")]
    public int? ParentId { get; set; }

    [JsonPropertyName("memberIds")]
    public List<int> MemberIds { get; set; } = [];

    public StructuredLocationDto? Location { get; set; }

    [JsonPropertyName("doc_markdown")]
    public string? DocMarkdown { get; set; }

    public ItemDocStructuredDto? Doc { get; set; }

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
