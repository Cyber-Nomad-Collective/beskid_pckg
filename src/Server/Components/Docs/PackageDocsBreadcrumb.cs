using Server.Contracts.ApiDocumentation;

namespace Server.Components.Docs;

public sealed record PackageDocsBreadcrumb(string Label, StructuredApiItemDto? Item, bool Selectable);
