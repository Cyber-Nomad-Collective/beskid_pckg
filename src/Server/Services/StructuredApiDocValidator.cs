using System.Text.Json;
using Server.Contracts.ApiDocumentation;

namespace Server.Services;

/// <summary>Validates packed <c>api.json</c> for registry publish and docs UI.</summary>
public static class StructuredApiDocValidator
{
    private const int MaxGraphRoots = 128;

    public static (bool IsValid, string Message) ValidateJson(string json)
    {
        StructuredApiDocDto? doc;
        try
        {
            doc = JsonSerializer.Deserialize<StructuredApiDocDto>(json, StructuredApiDocJson.Options);
        }
        catch (JsonException ex)
        {
            return (false, $"api.json is not valid JSON: {ex.Message}");
        }

        if (doc is null || doc.Items.Count == 0)
        {
            return (false, "api.json must contain at least one item.");
        }

        if (doc.SchemaVersion < 3)
        {
            return (false, "api.json schemaVersion must be >= 3 for graph navigation.");
        }

        if (!string.Equals(
                doc.NavigationModel,
                "graph-v1",
                StringComparison.Ordinal))
        {
            return (false, "api.json navigationModel must be \"graph-v1\".");
        }

        if (!ApiDocArtifactPathValidation.IsArtifactRelative(doc.Source))
        {
            return (false, "api.json source must be artifact-relative (forward slashes, same paths as in the .bpk).");
        }

        foreach (var item in doc.Items)
        {
            if (item.Id is null)
            {
                return (false, "api.json graph items must include id.");
            }

            if (item.ParentId is int pid
                && !doc.Items.Any(i => i.Id == pid))
            {
                return (
                    false,
                    $"api.json item \"{item.QualifiedName}\" references missing parentId {pid}.");
            }

            if (item.Location is { File: { } file }
                && !ApiDocArtifactPathValidation.IsArtifactRelative(file))
            {
                return (
                    false,
                    $"api.json item \"{item.QualifiedName}\" location.file must be artifact-relative (forward slashes, same paths as in the .bpk).");
            }

            if (string.Equals(item.Kind, "module", StringComparison.OrdinalIgnoreCase)
                && item.ModulePath.Count > 1
                && item.ParentId is null)
            {
                return (
                    false,
                    $"api.json module \"{item.QualifiedName}\" must have parentId (library tree).");
            }
        }

        var roots = doc.Items.Count(i => i.ParentId is null);
        if (roots > MaxGraphRoots)
        {
            return (
                false,
                $"api.json has {roots} graph roots (max {MaxGraphRoots}). "
                + "Re-run `beskid doc` with a current Beskid CLI to link the module library tree.");
        }

        return (true, string.Empty);
    }
}
