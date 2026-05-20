using System.Text.RegularExpressions;

namespace Server.Services.Workspace;

public sealed record WorkspaceMemberDefinition(string MemberId, string RelativePath);

public sealed record WorkspaceManifestDefinition(
    string WorkspaceName,
    IReadOnlyList<WorkspaceMemberDefinition> Members,
    IReadOnlyDictionary<string, string> VersionOverrides);

public sealed record ProjectDependencyDefinition(
    string Name,
    string Source,
    string? Path,
    string? Version,
    string? Registry);

public sealed record ProjectManifestLite(
    string ProjectName,
    IReadOnlyList<ProjectDependencyDefinition> Dependencies);

public static class WorkspaceManifestParsing
{
    private static readonly Regex MemberBlockRegex = new(
        @"member\s+""([^""]+)""\s*\{([^}]*)\}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex OverrideBlockRegex = new(
        @"override\s+""([^""]+)""\s*\{([^}]*)\}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex DependencyBlockRegex = new(
        @"dependency\s+""([^""]+)""\s*\{([^}]*)\}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex FieldRegex = new(
        @"(\w+)\s*=\s*""([^""]*)""",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static WorkspaceManifestDefinition ParseWorkspaceProj(string source)
    {
        var workspaceMatch = Regex.Match(
            source,
            @"workspace\s*\{([^}]*)\}",
            RegexOptions.CultureInvariant);
        if (!workspaceMatch.Success)
        {
            throw new InvalidOperationException("Workspace.proj is missing a workspace block.");
        }

        var workspaceFields = ParseFields(workspaceMatch.Groups[1].Value);
        var workspaceName = workspaceFields.GetValueOrDefault("name")
            ?? throw new InvalidOperationException("Workspace.proj workspace block is missing name.");

        var members = new List<WorkspaceMemberDefinition>();
        foreach (Match match in MemberBlockRegex.Matches(source))
        {
            var memberId = match.Groups[1].Value.Trim();
            var fields = ParseFields(match.Groups[2].Value);
            var path = fields.GetValueOrDefault("path")
                ?? throw new InvalidOperationException($"Workspace member '{memberId}' is missing path.");
            members.Add(new WorkspaceMemberDefinition(memberId, NormalizeRelativePath(path)));
        }

        if (members.Count == 0)
        {
            throw new InvalidOperationException("Workspace.proj must declare at least one member.");
        }

        var overrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in OverrideBlockRegex.Matches(source))
        {
            var dependency = match.Groups[1].Value.Trim();
            var fields = ParseFields(match.Groups[2].Value);
            if (fields.TryGetValue("version", out var version) && !string.IsNullOrWhiteSpace(version))
            {
                overrides[dependency] = version;
            }
        }

        return new WorkspaceManifestDefinition(workspaceName, members, overrides);
    }

    public static ProjectManifestLite ParseProjectProj(string source)
    {
        var projectMatch = Regex.Match(
            source,
            @"project\s*\{([^}]*)\}",
            RegexOptions.CultureInvariant);
        if (!projectMatch.Success)
        {
            throw new InvalidOperationException("Project.proj is missing a project block.");
        }

        var projectFields = ParseFields(projectMatch.Groups[1].Value);
        var projectName = projectFields.GetValueOrDefault("name")
            ?? throw new InvalidOperationException("Project.proj project block is missing name.");

        var dependencies = new List<ProjectDependencyDefinition>();
        foreach (Match match in DependencyBlockRegex.Matches(source))
        {
            var name = match.Groups[1].Value.Trim();
            var fields = ParseFields(match.Groups[2].Value);
            var sourceKind = fields.GetValueOrDefault("source")
                ?? throw new InvalidOperationException($"dependency '{name}' is missing source.");
            dependencies.Add(new ProjectDependencyDefinition(
                name,
                sourceKind,
                fields.GetValueOrDefault("path"),
                fields.GetValueOrDefault("version"),
                fields.GetValueOrDefault("registry")));
        }

        return new ProjectManifestLite(projectName, dependencies);
    }

    public static string RewriteProjectProjDependencies(
        string source,
        Func<ProjectDependencyDefinition, ProjectDependencyDefinition?> rewrite)
    {
        return DependencyBlockRegex.Replace(
            source,
            match =>
            {
                var name = match.Groups[1].Value.Trim();
                var fields = ParseFields(match.Groups[2].Value);
                var sourceKind = fields.GetValueOrDefault("source") ?? "path";
                var current = new ProjectDependencyDefinition(
                    name,
                    sourceKind,
                    fields.GetValueOrDefault("path"),
                    fields.GetValueOrDefault("version"),
                    fields.GetValueOrDefault("registry"));

                var rewritten = rewrite(current);
                if (rewritten is null)
                {
                    return match.Value;
                }

                return RenderDependencyBlock(rewritten);
            });
    }

    private static string RenderDependencyBlock(ProjectDependencyDefinition dependency)
    {
        var lines = new List<string>
        {
            $"dependency \"{dependency.Name}\" {{",
            $"  source = \"{dependency.Source}\"",
        };

        if (!string.IsNullOrWhiteSpace(dependency.Version))
        {
            lines.Add($"  version = \"{dependency.Version}\"");
        }

        if (!string.IsNullOrWhiteSpace(dependency.Registry))
        {
            lines.Add($"  registry = \"{dependency.Registry}\"");
        }

        if (!string.IsNullOrWhiteSpace(dependency.Path)
            && string.Equals(dependency.Source, "path", StringComparison.OrdinalIgnoreCase))
        {
            lines.Add($"  path = \"{dependency.Path}\"");
        }

        lines.Add("}");
        return string.Join('\n', lines);
    }

    private static Dictionary<string, string> ParseFields(string blockBody)
    {
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in FieldRegex.Matches(blockBody))
        {
            fields[match.Groups[1].Value] = match.Groups[2].Value;
        }

        return fields;
    }

    public static string NormalizeRelativePath(string path)
        => path.Replace('\\', '/').Trim().TrimStart('/').TrimEnd('/');

    public static string NormalizeZipEntryPath(string path)
        => path.Replace('\\', '/').TrimStart('/');
}
