using System.Text.Json;
using System.Text.Json.Nodes;

namespace Server.Services.Workspace;

public sealed record WorkspacePublishMemberConfig(
    string? PackageId,
    JsonObject? Configuration,
    IReadOnlyDictionary<string, string> Overrides);

public sealed record WorkspacePublishManifest(
    IReadOnlyDictionary<string, WorkspacePublishMemberConfig> Members,
    IReadOnlyDictionary<string, string> GlobalOverrides);

public static class WorkspacePackageManifest
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static WorkspacePublishManifest ReadWorkspacePackageJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new WorkspacePublishManifest(
                new Dictionary<string, WorkspacePublishMemberConfig>(),
                new Dictionary<string, string>());
        }

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.TryGetProperty("schema", out var schemaProp)
            && schemaProp.ValueKind == JsonValueKind.String
            && !string.Equals(schemaProp.GetString(), "beskid.workspace.package.v1", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("workspace.package.json schema must be 'beskid.workspace.package.v1'.");
        }

        var globalOverrides = ReadStringMap(root, "overrides");
        var members = new Dictionary<string, WorkspacePublishMemberConfig>(StringComparer.OrdinalIgnoreCase);

        if (root.TryGetProperty("members", out var membersElement) && membersElement.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in membersElement.EnumerateObject())
            {
                members[property.Name] = ReadMemberConfig(property.Value);
            }
        }

        return new WorkspacePublishManifest(members, globalOverrides);
    }

    public static PackagePckgSection ReadPackagePckgSection(string? packageJsonText)
    {
        if (string.IsNullOrWhiteSpace(packageJsonText))
        {
            return PackagePckgSection.Empty;
        }

        using var doc = JsonDocument.Parse(packageJsonText);
        if (!doc.RootElement.TryGetProperty("pckg", out var pckg) || pckg.ValueKind != JsonValueKind.Object)
        {
            return PackagePckgSection.Empty;
        }

        JsonObject? configuration = null;
        if (pckg.TryGetProperty("configuration", out var configurationElement)
            && configurationElement.ValueKind == JsonValueKind.Object)
        {
            configuration = JsonNode.Parse(configurationElement.GetRawText())?.AsObject();
        }

        var overrides = ReadStringMap(pckg, "overrides");
        var packageId = pckg.TryGetProperty("packageId", out var packageIdElement)
            && packageIdElement.ValueKind == JsonValueKind.String
            ? packageIdElement.GetString()
            : null;

        return new PackagePckgSection(packageId, configuration, overrides);
    }

    public static string MergePublishedPackageJson(
        string? existingPackageJson,
        string packageId,
        string publishVersion,
        IReadOnlyDictionary<string, PublishedRegistryDependency> registryDependencies,
        PackagePckgSection pckgSection)
    {
        var root = string.IsNullOrWhiteSpace(existingPackageJson)
            ? new JsonObject()
            : JsonNode.Parse(existingPackageJson)?.AsObject() ?? new JsonObject();

        root["schema"] = "beskid.package.v1";
        root["id"] = packageId;
        root["version"] = publishVersion;

        var dependenciesNode = new JsonObject();
        foreach (var dependency in registryDependencies.Values.OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase))
        {
            dependenciesNode[dependency.Name] = new JsonObject
            {
                ["version"] = dependency.Version,
                ["source"] = "registry",
            };
        }

        root["dependencies"] = dependenciesNode;

        if (pckgSection.Configuration is not null || pckgSection.Overrides.Count > 0 || pckgSection.PackageId is not null)
        {
            var pckgNode = new JsonObject();
            if (pckgSection.Configuration is not null)
            {
                pckgNode["configuration"] = pckgSection.Configuration.DeepClone();
            }

            if (pckgSection.Overrides.Count > 0)
            {
                var overridesNode = new JsonObject();
                foreach (var pair in pckgSection.Overrides.OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase))
                {
                    overridesNode[pair.Key] = pair.Value;
                }

                pckgNode["overrides"] = overridesNode;
            }

            if (!string.IsNullOrWhiteSpace(pckgSection.PackageId))
            {
                pckgNode["packageId"] = pckgSection.PackageId;
            }

            root["pckg"] = pckgNode;
        }

        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
    }

    private static WorkspacePublishMemberConfig ReadMemberConfig(JsonElement element)
    {
        var packageId = element.TryGetProperty("package", out var packageElement)
            && packageElement.ValueKind == JsonValueKind.String
            ? packageElement.GetString()
            : element.TryGetProperty("packageId", out var packageIdElement)
                && packageIdElement.ValueKind == JsonValueKind.String
                ? packageIdElement.GetString()
                : null;

        JsonObject? configuration = null;
        if (element.TryGetProperty("configuration", out var configurationElement)
            && configurationElement.ValueKind == JsonValueKind.Object)
        {
            configuration = JsonNode.Parse(configurationElement.GetRawText())?.AsObject();
        }

        var overrides = ReadStringMap(element, "overrides");
        return new WorkspacePublishMemberConfig(packageId, configuration, overrides);
    }

    private static IReadOnlyDictionary<string, string> ReadStringMap(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var element) || element.ValueKind != JsonValueKind.Object)
        {
            return new Dictionary<string, string>();
        }

        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in element.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.String)
            {
                var value = property.Value.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    map[property.Name] = value;
                }
            }
        }

        return map;
    }
}

public sealed record PackagePckgSection(
    string? PackageId,
    JsonObject? Configuration,
    IReadOnlyDictionary<string, string> Overrides)
{
    public static PackagePckgSection Empty { get; } = new(null, null, new Dictionary<string, string>());
}

public sealed record PublishedRegistryDependency(string Name, string Version);
