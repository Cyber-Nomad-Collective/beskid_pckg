using System.Security.Claims;
using FastEndpoints;
using Server.Services;

namespace Server.Features.Admin;

public sealed class ManagePermissionsEndpoint : Endpoint<ManagePermissionsRequest>
{
    public IAuthorizationService AuthService { get; set; } = default!;

    public override void Configure()
    {
        Post("/admin/permissions");
        Roles("SuperAdmin");
    }

    public override async Task HandleAsync(ManagePermissionsRequest req, CancellationToken ct)
    {
        var grantedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(grantedByUserId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        if (!string.IsNullOrWhiteSpace(req.Subject)
            || !string.IsNullOrWhiteSpace(req.Resource)
            || !string.IsNullOrWhiteSpace(req.Capability))
        {
            var resource = ReactAdminPermission.ParseResource(req.Resource);
            if (string.IsNullOrWhiteSpace(req.Subject)
                || string.IsNullOrWhiteSpace(req.Capability)
                || resource is null)
            {
                AddError("Request must contain subject, resource in '<type>:<id>' form, and capability.");
                await Send.ErrorsAsync(StatusCodes.Status400BadRequest, ct);
                return;
            }

            await AuthService.GrantPermissionAsync(
                req.Subject,
                resource.Value.Type,
                resource.Value.Id,
                req.Capability.Trim(),
                grantedByUserId);
            await Send.ResponseAsync(
                new ReactAdminPermission(
                    req.Subject,
                    resource.Value.Type.ToLowerInvariant() + ":" + resource.Value.Id,
                    req.Capability.Trim()),
                StatusCodes.Status201Created,
                ct);
            return;
        }

        if (req.Action == "grant")
        {
            await AuthService.GrantPermissionAsync(
                req.UserId, 
                req.ResourceType, 
                req.ResourceId, 
                req.Permission, 
                grantedByUserId);
            
            await Send.OkAsync(new ManagePermissionsResponse(true, "Permission granted."), ct);
        }
        else if (req.Action == "revoke")
        {
            await AuthService.RevokePermissionAsync(
                req.UserId, 
                req.ResourceType, 
                req.ResourceId, 
                req.Permission);
            
            await Send.OkAsync(new ManagePermissionsResponse(true, "Permission revoked."), ct);
        }
        else
        {
            await Send.ResponseAsync(new ManagePermissionsResponse(false, "Invalid action."), StatusCodes.Status400BadRequest, ct);
        }
    }
}

public sealed class ManagePermissionsRequest
{
    public string Action { get; init; } = string.Empty;
    public string UserId { get; init; } = string.Empty;
    public string ResourceType { get; init; } = string.Empty;
    public string ResourceId { get; init; } = string.Empty;
    public string Permission { get; init; } = string.Empty;
    public string Subject { get; init; } = string.Empty;
    public string Resource { get; init; } = string.Empty;
    public string Capability { get; init; } = string.Empty;
}

public sealed record ManagePermissionsResponse(bool Success, string Message);
