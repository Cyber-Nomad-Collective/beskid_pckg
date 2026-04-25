namespace Server.Components.Docs;

public sealed record PackageDocsSymbolRow(
    int? Id,
    string DisplayName,
    string Kind,
    string Module,
    string Visibility,
    string? Location,
    StructuredApiItemDto Item);

public sealed record PackageDocsTocRow(
    int Level,
    string Title,
    string Anchor);
