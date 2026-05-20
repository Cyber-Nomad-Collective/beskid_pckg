using Microsoft.EntityFrameworkCore;
using Server.Data;
using Server.Features.Packages;

namespace Server.Services.Workspace;

public sealed record WorkspacePublishMemberResult(
    string MemberId,
    string PackageName,
    string Version,
    string ChecksumSha256,
    long SizeBytes);

public sealed record WorkspacePublishOperationResult(
    bool Success,
    string Message,
    string? WorkspaceName,
    IReadOnlyList<WorkspacePublishMemberResult> Packages,
    int StatusCode);

public interface IWorkspacePublishService
{
    Task<WorkspacePublishOperationResult> PublishAsync(
        Stream workspaceBundle,
        string userId,
        RegistryVersionBump versionBump,
        CancellationToken cancellationToken = default);
}

public sealed class WorkspacePublishService(
    ApplicationDbContext dbContext,
    IPackagePublishService packagePublishService,
    ILogger<WorkspacePublishService> logger) : IWorkspacePublishService
{
    public async Task<WorkspacePublishOperationResult> PublishAsync(
        Stream workspaceBundle,
        string userId,
        RegistryVersionBump versionBump,
        CancellationToken cancellationToken = default)
    {
        WorkspaceBundle bundle;
        try
        {
            bundle = WorkspaceBundle.FromZip(workspaceBundle);
        }
        catch (InvalidOperationException ex)
        {
            return Failure(ex.Message, StatusCodes.Status400BadRequest);
        }

        WorkspaceManifestDefinition workspaceManifest;
        try
        {
            var workspaceProj = bundle.RequireText("Workspace.proj");
            workspaceManifest = WorkspaceManifestParsing.ParseWorkspaceProj(workspaceProj);
        }
        catch (InvalidOperationException ex)
        {
            return Failure(ex.Message, StatusCodes.Status400BadRequest);
        }

        WorkspacePublishManifest workspacePackageManifest;
        try
        {
            var workspacePackageJson = bundle.TryGetEntry("workspace.package.json", out var json)
                ? System.Text.Encoding.UTF8.GetString(json)
                : null;
            workspacePackageManifest = WorkspacePackageManifest.ReadWorkspacePackageJson(workspacePackageJson);
        }
        catch (InvalidOperationException ex)
        {
            return Failure(ex.Message, StatusCodes.Status400BadRequest);
        }

        var memberContexts = new List<WorkspaceMemberPublishContext>();
        foreach (var member in workspaceManifest.Members)
        {
            try
            {
                memberContexts.Add(BuildMemberContext(
                    bundle,
                    member,
                    workspaceManifest,
                    workspacePackageManifest));
            }
            catch (InvalidOperationException ex)
            {
                return Failure($"Member '{member.MemberId}': {ex.Message}", StatusCodes.Status400BadRequest);
            }
        }

        IReadOnlyList<PackageEntity> packages;
        try
        {
            packages = await WorkspacePackageProvisioning.EnsureOwnedPackagesAsync(
                dbContext,
                userId,
                memberContexts,
                workspacePackageManifest,
                cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            var statusCode = ex.Message.Contains("do not own", StringComparison.OrdinalIgnoreCase)
                ? StatusCodes.Status403Forbidden
                : StatusCodes.Status400BadRequest;
            return Failure(ex.Message, statusCode);
        }

        var assignedVersions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var package in packages)
        {
            var nonYankedVersions = await dbContext.PackageVersions
                .AsNoTracking()
                .Where(x => x.PackageId == package.Id && !x.IsYanked)
                .Select(x => x.Version)
                .ToListAsync(cancellationToken);
            assignedVersions[package.Name] = PackageVersioning.ComputeNextVersion(nonYankedVersions, versionBump);
        }

        var membersWithVersions = memberContexts
            .Select(member => member with { AssignedVersion = assignedVersions[member.PackageId] })
            .ToList();

        var index = new WorkspaceMemberIndex(membersWithVersions);
        IReadOnlyList<WorkspaceMemberPublishContext> orderedMembers;
        try
        {
            orderedMembers = WorkspaceDependencyRewriter.OrderMembersForPublish(membersWithVersions, index);
        }
        catch (InvalidOperationException ex)
        {
            return Failure(ex.Message, StatusCodes.Status400BadRequest);
        }

        var published = new Dictionary<string, WorkspaceMemberPublishContext>(StringComparer.OrdinalIgnoreCase);
        var results = new List<WorkspacePublishMemberResult>();

        try
        {
            await PublishOrderedMembersAsync(
                orderedMembers,
                packages,
                bundle,
                index,
                published,
                workspaceManifest,
                workspacePackageManifest,
                userId,
                results,
                cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning("Workspace publish validation failed: {Message}", ex.Message);
            return Failure(ex.Message, StatusCodes.Status400BadRequest);
        }

        return new WorkspacePublishOperationResult(
            true,
            "Workspace packages published.",
            workspaceManifest.WorkspaceName,
            results,
            StatusCodes.Status200OK);
    }

    private static WorkspaceMemberPublishContext BuildMemberContext(
        WorkspaceBundle bundle,
        WorkspaceMemberDefinition member,
        WorkspaceManifestDefinition workspaceManifest,
        WorkspacePublishManifest workspacePackageManifest)
    {
        var projectProjPath = $"{member.RelativePath}/Project.proj";
        var projectProj = bundle.RequireText(projectProjPath);
        var projectManifest = WorkspaceManifestParsing.ParseProjectProj(projectProj);

        var packageJsonPath = $"{member.RelativePath}/package.json";
        var existingPackageJson = bundle.TryGetEntry(packageJsonPath, out var packageJsonBytes)
            ? System.Text.Encoding.UTF8.GetString(packageJsonBytes)
            : null;
        var packagePckg = WorkspacePackageManifest.ReadPackagePckgSection(existingPackageJson);

        workspacePackageManifest.Members.TryGetValue(member.MemberId, out var memberConfig);
        var packageId = memberConfig?.PackageId
            ?? packagePckg.PackageId
            ?? TryReadPackageJsonId(existingPackageJson)
            ?? projectManifest.ProjectName;

        var effectiveOverrides = MergeOverrides(
            workspaceManifest.VersionOverrides,
            workspacePackageManifest.GlobalOverrides,
            memberConfig?.Overrides ?? new Dictionary<string, string>(),
            packagePckg.Overrides);

        return new WorkspaceMemberPublishContext(
            member.MemberId,
            member.RelativePath,
            packageId,
            projectManifest.ProjectName,
            "0.0.0",
            projectManifest,
            packagePckg with
            {
                Configuration = memberConfig?.Configuration ?? packagePckg.Configuration,
            },
            effectiveOverrides);
    }

    private static string? TryReadPackageJsonId(string? packageJson)
    {
        if (string.IsNullOrWhiteSpace(packageJson))
        {
            return null;
        }

        using var doc = System.Text.Json.JsonDocument.Parse(packageJson);
        return doc.RootElement.TryGetProperty("id", out var idElement)
            && idElement.ValueKind == System.Text.Json.JsonValueKind.String
            ? idElement.GetString()
            : null;
    }

    private static IReadOnlyDictionary<string, string> MergeOverrides(
        params IReadOnlyDictionary<string, string>[] maps)
    {
        var merged = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var map in maps)
        {
            foreach (var pair in map)
            {
                merged[pair.Key] = pair.Value;
            }
        }

        return merged;
    }

    private async Task PublishOrderedMembersAsync(
        IReadOnlyList<WorkspaceMemberPublishContext> orderedMembers,
        IReadOnlyList<PackageEntity> packages,
        WorkspaceBundle bundle,
        WorkspaceMemberIndex index,
        Dictionary<string, WorkspaceMemberPublishContext> published,
        WorkspaceManifestDefinition workspaceManifest,
        WorkspacePublishManifest workspacePackageManifest,
        string userId,
        List<WorkspacePublishMemberResult> results,
        CancellationToken cancellationToken)
    {
        var workspaceOverrides = MergeOverrides(
            workspaceManifest.VersionOverrides,
            workspacePackageManifest.GlobalOverrides);

        async Task PublishLoopAsync()
        {
            foreach (var member in orderedMembers)
            {
                var package = packages.Single(p =>
                    string.Equals(p.Name, member.PackageId, StringComparison.OrdinalIgnoreCase));

                string? existingPackageJson = bundle.TryGetEntry(
                    $"{member.MemberRelativePath}/package.json",
                    out var packageJsonBytes)
                    ? System.Text.Encoding.UTF8.GetString(packageJsonBytes)
                    : null;

                var registryDependencies = WorkspaceDependencyRewriter.BuildRegistryDependencies(
                    member,
                    index,
                    published,
                    workspaceOverrides,
                    existingPackageJson);

                var projectProjSource = System.Text.Encoding.UTF8.GetString(
                    bundle.CollectMemberPackEntries(member.MemberRelativePath)["Project.proj"]);
                var rewrittenProjectProj = WorkspaceDependencyRewriter.RewriteProjectProj(
                    projectProjSource,
                    member,
                    index,
                    published,
                    workspaceOverrides);

                var memberEntries = bundle.CollectMemberPackEntries(member.MemberRelativePath);
                PackagePublishDocumentation.EnsureStructuredApiDoc(memberEntries, member.PackageId);

                var packageJson = WorkspacePackageManifest.MergePublishedPackageJson(
                    existingPackageJson,
                    member.PackageId,
                    member.AssignedVersion,
                    registryDependencies,
                    member.PackagePckg,
                    PackagePublishDocumentation.HasStructuredApiDoc(memberEntries));

                var artifactBytes = WorkspaceMemberArtifactBuilder.BuildArtifact(
                    memberEntries,
                    packageJson,
                    rewrittenProjectProj);

                await using var artifactStream = new MemoryStream(artifactBytes);
                var publishResult = await packagePublishService.PublishAsync(
                    new PackagePublishRequest(
                        package,
                        member.AssignedVersion,
                        artifactStream,
                        RelaxPackageJsonVersion: true,
                        ExpectedChecksum: null,
                        ContentType: "application/zip",
                        userId),
                    cancellationToken);

                if (!publishResult.Success || publishResult.Version is null)
                {
                    throw new WorkspacePublishAbortException(
                        $"Failed to publish {member.PackageId}: {publishResult.Message}",
                        publishResult.StatusCode);
                }

                published[member.PackageId] = member;
                results.Add(new WorkspacePublishMemberResult(
                    member.MemberId,
                    member.PackageId,
                    publishResult.Version.Version,
                    publishResult.Version.ChecksumSha256,
                    publishResult.Version.SizeBytes));
            }
        }

        if (dbContext.Database.IsRelational())
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                await PublishLoopAsync();
                await transaction.CommitAsync(cancellationToken);
            }
            catch (WorkspacePublishAbortException ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                throw new InvalidOperationException(ex.Message);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }

            return;
        }

        try
        {
            await PublishLoopAsync();
        }
        catch (WorkspacePublishAbortException ex)
        {
            throw new InvalidOperationException(ex.Message);
        }
    }

    private sealed class WorkspacePublishAbortException(string message, int statusCode) : Exception(message)
    {
        public int StatusCode { get; } = statusCode;
    }

    private static WorkspacePublishOperationResult Failure(string message, int statusCode)
        => new(false, message, null, [], statusCode);
}
