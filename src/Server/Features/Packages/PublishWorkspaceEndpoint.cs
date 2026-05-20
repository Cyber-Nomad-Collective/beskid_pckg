using FastEndpoints;
using Server.Services;
using Server.Services.Workspace;

namespace Server.Features.Packages;

public sealed class PublishWorkspaceEndpoint(
    IApiPrincipalResolver principalResolver,
    IWorkspacePublishService workspacePublishService,
    IPckgRegistryActivityLog registryActivity,
    ILogger<PublishWorkspaceEndpoint> logger)
    : EndpointWithoutRequest<PublishWorkspaceResponse>
{
    private const long MaxWorkspaceBundleBytes = 128 * 1024 * 1024;

    public override void Configure()
    {
        Post("/workspaces/publish");
        Options(x =>
        {
            x.RequireAuthorization();
            x.RequireRateLimiting("publish");
        });
        Summary(s =>
        {
            s.Summary = "Publish all packages from a workspace bundle.";
            s.Description =
                "Multipart: artifact (required) — ZIP containing Workspace.proj and member source trees. " +
                "versionBump (optional patch|minor|major, default patch) applies registry-assigned versions to every member package.";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var userId = await principalResolver.ResolveUserIdAsync(HttpContext, ct);
        if (string.IsNullOrWhiteSpace(userId))
        {
            await Send.ResponseAsync(
                new PublishWorkspaceResponse(false, "Unauthorized.", null, []),
                StatusCodes.Status401Unauthorized,
                ct);
            return;
        }

        if (!HttpContext.Request.HasFormContentType)
        {
            await Send.ResponseAsync(
                new PublishWorkspaceResponse(false, "Expected multipart form payload.", null, []),
                StatusCodes.Status400BadRequest,
                ct);
            return;
        }

        var form = await HttpContext.Request.ReadFormAsync(ct);
        var versionBump = PackageVersioning.ParseBump(form["versionBump"].FirstOrDefault());
        var artifact = form.Files.GetFile("artifact");
        if (artifact is null)
        {
            await Send.ResponseAsync(
                new PublishWorkspaceResponse(false, "Artifact file is required.", null, []),
                StatusCodes.Status400BadRequest,
                ct);
            return;
        }

        if (artifact.Length <= 0 || artifact.Length > MaxWorkspaceBundleBytes)
        {
            await Send.ResponseAsync(
                new PublishWorkspaceResponse(
                    false,
                    $"Artifact size must be between 1 byte and {MaxWorkspaceBundleBytes} bytes.",
                    null,
                    []),
                StatusCodes.Status400BadRequest,
                ct);
            return;
        }

        await using var artifactStream = artifact.OpenReadStream();
        var result = await workspacePublishService.PublishAsync(artifactStream, userId, versionBump, ct);

        registryActivity.Append(new RegistryActivityEntry(
            DateTimeOffset.UtcNow,
            result.Success ? "Information" : "Warning",
            result.Success ? "workspace_publish_success" : "workspace_publish_failed",
            result.Message,
            HttpContext.TraceIdentifier,
            userId,
            result.WorkspaceName,
            null));

        if (!result.Success)
        {
            logger.LogWarning(
                "Workspace publish rejected for user {UserId}: {Message}",
                userId,
                result.Message);
            await Send.ResponseAsync(
                new PublishWorkspaceResponse(false, result.Message, result.WorkspaceName, []),
                result.StatusCode,
                ct);
            return;
        }

        var packages = result.Packages
            .Select(package => new PublishWorkspaceMemberResponse(
                package.MemberId,
                package.PackageName,
                package.Version,
                package.ChecksumSha256,
                package.SizeBytes))
            .ToList();

        await Send.OkAsync(
            new PublishWorkspaceResponse(true, result.Message, result.WorkspaceName, packages),
            ct);
    }
}
