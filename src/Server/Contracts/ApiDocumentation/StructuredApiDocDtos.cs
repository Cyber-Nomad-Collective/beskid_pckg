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

public sealed class ApiTypeAnnotationDto
{
    [JsonPropertyName("display")]
    public string Display { get; set; } = string.Empty;

    [JsonPropertyName("refItemId")]
    public int? RefItemId { get; set; }
}

public sealed class ApiParameterDocDto
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public ApiTypeAnnotationDto Ty { get; set; } = new();

    [JsonPropertyName("modifier")]
    public string? Modifier { get; set; }

    [JsonPropertyName("docMarkdown")]
    public string? DocMarkdown { get; set; }
}

public sealed class ApiGenericParameterDocDto
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

public sealed class StructuredApiItemDto
{
    public int? Id { get; set; }

    [JsonPropertyName("qualifiedName")]
    public string? QualifiedName { get; set; }

    public string? Name { get; set; }

    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }

    public string? Kind { get; set; }

    public string? Visibility { get; set; }

    [JsonPropertyName("parentId")]
    public int? ParentId { get; set; }

    [JsonPropertyName("memberIds")]
    public List<int> MemberIds { get; set; } = [];

    [JsonPropertyName("modulePath")]
    public List<string> ModulePath { get; set; } = [];

    public string? Signature { get; set; }

    [JsonPropertyName("fieldType")]
    public ApiTypeAnnotationDto? FieldType { get; set; }

    [JsonPropertyName("returnType")]
    public ApiTypeAnnotationDto? ReturnType { get; set; }

    public List<ApiParameterDocDto> Parameters { get; set; } = [];

    [JsonPropertyName("genericParameters")]
    public List<ApiGenericParameterDocDto> GenericParameters { get; set; } = [];

    public StructuredLocationDto? Location { get; set; }

    [JsonPropertyName("docMarkdown")]
    public string? DocMarkdown { get; set; }

    [JsonPropertyName("doc_markdown")]
    public string? DocMarkdownLegacy
    {
        set => DocMarkdown ??= value;
    }

    public ItemDocStructuredDto? Doc { get; set; }

    [JsonPropertyName("declaringPackage")]
    public string? DeclaringPackage { get; set; }

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
