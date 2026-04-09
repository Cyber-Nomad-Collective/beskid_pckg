using System.Text.Json;

namespace Server.Services;

public sealed record PackageDependencyDescriptor(
    string Name,
    string? Version,
    string Source,
    string? Registry);

public sealed record PackageManifestMetadata(
    string? Schema,
    string? PackageId,
    string? Version,
    string? Readme,
    IReadOnlyList<PackageDependencyDescriptor> Dependencies);

public static class PackageManifestMetadataReader
{
    public static PackageManifestMetadata Read(string? manifestJson)
    {
        if (string.IsNullOrWhiteSpace(manifestJson))
        {
            return Empty();
        }

        try
        {
            using var doc = JsonDocument.Parse(manifestJson);
            var root = doc.RootElement;
            var schema = ReadString(root, "schema");
            var id = ReadString(root, "id");
            var version = ReadString(root, "version");
            var readme = ReadString(root, "readme");
            var dependencies = ReadDependencies(root);
            return new PackageManifestMetadata(schema, id, version, readme, dependencies);
        }
        catch (JsonException)
        {
            return Empty();
        }
    }

    private static PackageManifestMetadata Empty()
        => new(null, null, null, null, []);

    private static string? ReadString(JsonElement root, string property)
        => root.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static IReadOnlyList<PackageDependencyDescriptor> ReadDependencies(JsonElement root)
    {
        if (!root.TryGetProperty("dependencies", out var dependencies))
        {
            return [];
        }

        var result = new List<PackageDependencyDescriptor>();

        if (dependencies.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in dependencies.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.String)
                {
                    result.Add(new PackageDependencyDescriptor(property.Name, property.Value.GetString(), "registry", null));
                    continue;
                }

                if (property.Value.ValueKind == JsonValueKind.Object)
                {
                    var version = ReadString(property.Value, "version");
                    var source = ReadString(property.Value, "source") ?? "registry";
                    var registry = ReadString(property.Value, "registry");
                    result.Add(new PackageDependencyDescriptor(property.Name, version, source, registry));
                }
            }
        }
        else if (dependencies.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in dependencies.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var name = ReadString(item, "name") ?? ReadString(item, "id");
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                var version = ReadString(item, "version");
                var source = ReadString(item, "source") ?? "registry";
                var registry = ReadString(item, "registry");
                result.Add(new PackageDependencyDescriptor(name, version, source, registry));
            }
        }

        return result
            .Where(d => !string.IsNullOrWhiteSpace(d.Name))
            .DistinctBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
            .OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
