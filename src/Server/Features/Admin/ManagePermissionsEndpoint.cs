using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using Server.Services;
using System.Security.Claims;

namespace Server.Features.Admin;

public sealed class ManagePermissionsEndpoint : Endpoint<ManagePermissionsRequest, ManagePermissionsResponse>
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

public sealed record ManagePermissionsRequest(
    string Action,
    string UserId,
    string ResourceType,
    string ResourceId,
    string Permission
);

public sealed record ManagePermissionsResponse(bool Success, string Message);
