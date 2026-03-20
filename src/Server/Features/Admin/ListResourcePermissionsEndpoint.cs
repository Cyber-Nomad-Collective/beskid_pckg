using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using Server.Data;

namespace Server.Features.Admin;

public sealed class ListResourcePermissionsEndpoint : EndpointWithoutRequest<ListResourcePermissionsResponse>
{
    public ApplicationDbContext Db { get; set; } = default!;

    public override void Configure()
    {
        Get("/admin/permissions/{resourceType}/{resourceId}");
        Roles("SuperAdmin");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var resourceType = Route<string>("resourceType");
        var resourceId = Route<string>("resourceId");

        var permissions = await Db.ResourcePermissions
            .Where(p => p.ResourceType == resourceType && p.ResourceId == resourceId)
            .Select(p => new PermissionDto(
                p.Id,
                p.UserId,
                p.Permission,
                p.GrantedByUserId,
                p.GrantedAtUtc
            ))
            .ToListAsync(ct);

        await Send.OkAsync(new ListResourcePermissionsResponse(permissions), ct);
    }
}

public sealed record ListResourcePermissionsResponse(List<PermissionDto> Permissions);

public sealed record PermissionDto(
    int Id,
    string UserId,
    string Permission,
    string GrantedByUserId,
    DateTime GrantedAtUtc
);
