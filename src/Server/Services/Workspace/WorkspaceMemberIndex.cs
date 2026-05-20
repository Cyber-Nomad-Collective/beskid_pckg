namespace Server.Services.Workspace;

public sealed class WorkspaceMemberIndex
{
    private readonly IReadOnlyDictionary<string, WorkspaceMemberPublishContext> _byMemberId;
    private readonly IReadOnlyDictionary<string, WorkspaceMemberPublishContext> _byPackageId;
    private readonly IReadOnlyDictionary<string, WorkspaceMemberPublishContext> _byProjectName;

    public WorkspaceMemberIndex(IReadOnlyList<WorkspaceMemberPublishContext> members)
    {
        Members = members;
        _byMemberId = members.ToDictionary(m => m.MemberId, StringComparer.OrdinalIgnoreCase);
        _byPackageId = members.ToDictionary(m => m.PackageId, StringComparer.OrdinalIgnoreCase);
        _byProjectName = members.ToDictionary(m => m.ProjectName, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<WorkspaceMemberPublishContext> Members { get; }

    public bool TryGetByMemberId(string id, out WorkspaceMemberPublishContext member)
        => _byMemberId.TryGetValue(id, out member!);

    public bool TryGetByPackageId(string id, out WorkspaceMemberPublishContext member)
        => _byPackageId.TryGetValue(id, out member!);

    public bool TryGetByProjectName(string name, out WorkspaceMemberPublishContext member)
        => _byProjectName.TryGetValue(name, out member!);

    public WorkspaceMemberPublishContext? ResolvePathDependencyTarget(
        WorkspaceMemberPublishContext consumer,
        ProjectDependencyDefinition dependency)
    {
        if (TryGetByProjectName(dependency.Name, out var byName))
        {
            return byName;
        }

        if (TryGetByPackageId(dependency.Name, out var byPackage))
        {
            return byPackage;
        }

        if (TryGetByMemberId(dependency.Name, out var byMember))
        {
            return byMember;
        }

        if (string.IsNullOrWhiteSpace(dependency.Path))
        {
            return null;
        }

        var dependencyPath = NormalizePath(dependency.Path);
        var consumerRoot = NormalizePath(consumer.MemberRelativePath);
        var resolvedTarget = NormalizePath(CombineRelative(consumerRoot, dependencyPath));

        foreach (var candidate in Members)
        {
            if (string.Equals(candidate.MemberId, consumer.MemberId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var candidateRoot = NormalizePath(candidate.MemberRelativePath);
            if (string.Equals(resolvedTarget, candidateRoot, StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        return null;
    }

    private static string NormalizePath(string path)
        => WorkspaceManifestParsing.NormalizeRelativePath(path);

    private static string CombineRelative(string root, string relative)
    {
        var segments = new List<string>();
        foreach (var segment in root.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            segments.Add(segment);
        }

        foreach (var segment in relative.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment == "..")
            {
                if (segments.Count == 0)
                {
                    throw new InvalidOperationException("Path dependency escapes workspace root.");
                }

                segments.RemoveAt(segments.Count - 1);
                continue;
            }

            if (segment == ".")
            {
                continue;
            }

            segments.Add(segment);
        }

        return string.Join('/', segments);
    }
}
