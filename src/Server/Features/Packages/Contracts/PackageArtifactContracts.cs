using System.Text.Json.Serialization;

namespace Server.Features.Packages;

public sealed record PackageDocFileEntry(
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("title")] string Title);

public sealed record PackageDocsIndexResponse(
    [property: JsonPropertyName("files")] IReadOnlyList<PackageDocFileEntry> Files,
    [property: JsonPropertyName("hasStructuredApiDoc")] bool HasStructuredApiDoc = false,
    [property: JsonPropertyName("structuredDocRelativePath")] string? StructuredDocRelativePath = null);

public sealed record PackageSourceTreeNodeResponse(
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("isDirectory")] bool IsDirectory,
    [property: JsonPropertyName("parentPath")] string? ParentPath,
    [property: JsonPropertyName("sizeBytes")] long? SizeBytes,
    [property: JsonPropertyName("fileType")] string FileType,
    [property: JsonPropertyName("iconKey")] string IconKey,
    [property: JsonPropertyName("previewKind")] string PreviewKind,
    [property: JsonPropertyName("monacoLanguage")] string? MonacoLanguage,
    [property: JsonPropertyName("contentType")] string? ContentType);

public sealed record PackageSourceTreeResponse(
    [property: JsonPropertyName("nodes")] IReadOnlyList<PackageSourceTreeNodeResponse> Nodes);
