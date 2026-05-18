using Server.Contracts.ApiDocumentation;

namespace Server.Components.Docs;

public sealed record PackageDocsSymbolRow(
    int? Id,
    string DisplayName,
    string Kind,
    string Parent,
    string Visibility,
    string? Location,
    StructuredApiItemDto Item);

public sealed record PackageDocsTocRow(
    int Level,
    string Title,
    string Anchor);
