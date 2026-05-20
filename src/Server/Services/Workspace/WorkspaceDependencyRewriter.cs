using System.Text.Json;

namespace Server.Services.Workspace;

public sealed record WorkspaceMemberPublishContext(
    string MemberId,
    string MemberRelativePath,
    string PackageId,
    string ProjectName,
    string AssignedVersion,
    ProjectManifestLite ProjectManifest,
    PackagePckgSection PackagePckg,
    IReadOnlyDictionary<string, string> EffectiveOverrides);

public static class WorkspaceDependencyRewriter
{
    public static IReadOnlyList<WorkspaceMemberPublishContext> OrderMembersForPublish(
        IReadOnlyList<WorkspaceMemberPublishContext> members,
        WorkspaceMemberIndex index)
    {
        var indegree = members.ToDictionary(m => m.MemberId, _ => 0, StringComparer.OrdinalIgnoreCase);
        var edges = members.ToDictionary(m => m.MemberId, _ => new List<string>(), StringComparer.OrdinalIgnoreCase);

        foreach (var member in members)
        {
            foreach (var dependency in member.ProjectManifest.Dependencies)
            {
                if (!string.Equals(dependency.Source, "path", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var target = index.ResolvePathDependencyTarget(member, dependency);
                if (target is null || string.Equals(target.MemberId, member.MemberId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                edges[target.MemberId].Add(member.MemberId);
                indegree[member.MemberId]++;
            }
        }

        var queue = new Queue<string>(
            indegree.Where(pair => pair.Value == 0).Select(pair => pair.Key).OrderBy(id => id, StringComparer.OrdinalIgnoreCase));
        var orderedIds = new List<string>();

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            orderedIds.Add(current);

            foreach (var dependent in edges[current].OrderBy(id => id, StringComparer.OrdinalIgnoreCase))
            {
                indegree[dependent]--;
                if (indegree[dependent] == 0)
                {
                    queue.Enqueue(dependent);
                }
            }
        }

        if (orderedIds.Count != members.Count)
        {
            throw new InvalidOperationException(
                "Workspace member dependency graph contains a cycle; path dependencies between members must be acyclic.");
        }

        var orderLookup = orderedIds
            .Select((id, index) => (id, index))
            .ToDictionary(pair => pair.id, pair => pair.index, StringComparer.OrdinalIgnoreCase);

        return members
            .OrderBy(member => orderLookup[member.MemberId])
            .ToList();
    }

    public static string RewriteProjectProj(
        string projectProjSource,
        WorkspaceMemberPublishContext member,
        WorkspaceMemberIndex index,
        IReadOnlyDictionary<string, WorkspaceMemberPublishContext> publishedVersions,
        IReadOnlyDictionary<string, string> workspaceOverrides)
    {
        return WorkspaceManifestParsing.RewriteProjectProjDependencies(
            projectProjSource,
            dependency =>
            {
                if (string.Equals(dependency.Source, "path", StringComparison.OrdinalIgnoreCase))
                {
                    var target = index.ResolvePathDependencyTarget(member, dependency);
                    if (target is not null
                        && publishedVersions.TryGetValue(target.PackageId, out var published))
                    {
                        return dependency with
                        {
                            Source = "registry",
                            Path = null,
                            Version = published.AssignedVersion,
                            Registry = dependency.Registry ?? "default",
                        };
                    }
                }
                else if (string.Equals(dependency.Source, "registry", StringComparison.OrdinalIgnoreCase))
                {
                    var version = ResolveRegistryDependencyVersion(dependency.Name, member, workspaceOverrides);
                    if (!string.IsNullOrWhiteSpace(version))
                    {
                        return dependency with { Version = version };
                    }
                }

                return dependency;
            });
    }

    public static IReadOnlyDictionary<string, PublishedRegistryDependency> BuildRegistryDependencies(
        WorkspaceMemberPublishContext member,
        WorkspaceMemberIndex index,
        IReadOnlyDictionary<string, WorkspaceMemberPublishContext> publishedVersions,
        IReadOnlyDictionary<string, string> workspaceOverrides,
        string? existingPackageJson)
    {
        var result = new Dictionary<string, PublishedRegistryDependency>(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(existingPackageJson))
        {
            foreach (var dependency in ReadPackageJsonDependencies(existingPackageJson))
            {
                if (IsWorkspaceInternalDependency(dependency.Source))
                {
                    continue;
                }

                if (!IsRegistryConsumerSource(dependency.Source))
                {
                    throw new InvalidOperationException(
                        $"package.json dependency '{dependency.Name}' must use registry source for published artifacts.");
                }

                var version = ResolveRegistryDependencyVersion(dependency.Name, member, workspaceOverrides)
                    ?? dependency.Version
                    ?? throw new InvalidOperationException(
                        $"package.json dependency '{dependency.Name}' is missing a version.");
                result[dependency.Name] = new PublishedRegistryDependency(dependency.Name, version);
            }
        }

        foreach (var dependency in member.ProjectManifest.Dependencies)
        {
            if (string.Equals(dependency.Source, "path", StringComparison.OrdinalIgnoreCase))
            {
                var target = index.ResolvePathDependencyTarget(member, dependency);
                if (target is not null
                    && publishedVersions.TryGetValue(target.PackageId, out var published))
                {
                    result[dependency.Name] = new PublishedRegistryDependency(dependency.Name, published.AssignedVersion);
                }

                continue;
            }

            if (!string.Equals(dependency.Source, "registry", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var version = ResolveRegistryDependencyVersion(dependency.Name, member, workspaceOverrides)
                ?? dependency.Version
                ?? throw new InvalidOperationException(
                    $"Project dependency '{dependency.Name}' is missing a version.");
            result[dependency.Name] = new PublishedRegistryDependency(dependency.Name, version);
        }

        return result;
    }

    private static string? ResolveRegistryDependencyVersion(
        string dependencyName,
        WorkspaceMemberPublishContext member,
        IReadOnlyDictionary<string, string> workspaceOverrides)
    {
        if (member.EffectiveOverrides.TryGetValue(dependencyName, out var memberOverride))
        {
            return memberOverride;
        }

        if (workspaceOverrides.TryGetValue(dependencyName, out var workspaceOverride))
        {
            return workspaceOverride;
        }

        return null;
    }

    private static IReadOnlyList<PackageJsonDependency> ReadPackageJsonDependencies(string packageJson)
    {
        using var doc = JsonDocument.Parse(packageJson);
        if (!doc.RootElement.TryGetProperty("dependencies", out var dependencies))
        {
            return [];
        }

        var result = new List<PackageJsonDependency>();
        if (dependencies.ValueKind != JsonValueKind.Object)
        {
            return result;
        }

        foreach (var property in dependencies.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.String)
            {
                result.Add(new PackageJsonDependency(property.Name, property.Value.GetString(), "registry"));
                continue;
            }

            if (property.Value.ValueKind == JsonValueKind.Object)
            {
                var version = property.Value.TryGetProperty("version", out var versionElement)
                    && versionElement.ValueKind == JsonValueKind.String
                    ? versionElement.GetString()
                    : null;
                var source = property.Value.TryGetProperty("source", out var sourceElement)
                    && sourceElement.ValueKind == JsonValueKind.String
                    ? sourceElement.GetString() ?? "registry"
                    : "registry";
                result.Add(new PackageJsonDependency(property.Name, version, source));
            }
        }

        return result;
    }

    private static bool IsWorkspaceInternalDependency(string? source)
        => string.Equals(source, "path", StringComparison.OrdinalIgnoreCase)
           || string.Equals(source, "workspace", StringComparison.OrdinalIgnoreCase);

    private static bool IsRegistryConsumerSource(string? source)
        => string.Equals(source, "registry", StringComparison.OrdinalIgnoreCase)
           || string.Equals(source, "pckg", StringComparison.OrdinalIgnoreCase);

    private sealed record PackageJsonDependency(string Name, string? Version, string Source);
}
