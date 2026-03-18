using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using pckg.Data;

namespace Server.Services;

public sealed class AuthorizationService : IAuthorizationService
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public AuthorizationService(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public async Task<bool> IsSuperAdminAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null) return false;
        return await _userManager.IsInRoleAsync(user, "SuperAdmin");
    }

    public async Task<bool> IsPackageOwnerAsync(string userId, int packageId)
    {
        var package = await _db.Packages.FindAsync(packageId);
        return package?.OwnerUserId == userId;
    }

    public async Task<bool> CanModerateAsync(string userId, string resourceType, string resourceId)
    {
        if (await IsSuperAdminAsync(userId))
            return true;

        if (resourceType == "Package")
        {
            if (int.TryParse(resourceId, out var packageId))
            {
                if (await IsPackageOwnerAsync(userId, packageId))
                    return true;
            }
        }

        return await HasPermissionAsync(userId, resourceType, resourceId, "Moderate");
    }

    public async Task<bool> HasPermissionAsync(string userId, string resourceType, string resourceId, string permission)
    {
        if (await IsSuperAdminAsync(userId))
            return true;

        return await _db.ResourcePermissions
            .AnyAsync(p => p.UserId == userId 
                && p.ResourceType == resourceType 
                && p.ResourceId == resourceId 
                && p.Permission == permission);
    }

    public async Task GrantPermissionAsync(string userId, string resourceType, string resourceId, string permission, string grantedByUserId)
    {
        var existing = await _db.ResourcePermissions
            .FirstOrDefaultAsync(p => p.UserId == userId 
                && p.ResourceType == resourceType 
                && p.ResourceId == resourceId 
                && p.Permission == permission);

        if (existing is not null)
            return;

        _db.ResourcePermissions.Add(new ResourcePermissionEntity
        {
            UserId = userId,
            ResourceType = resourceType,
            ResourceId = resourceId,
            Permission = permission,
            GrantedByUserId = grantedByUserId,
            GrantedAtUtc = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
    }

    public async Task RevokePermissionAsync(string userId, string resourceType, string resourceId, string permission)
    {
        var permission_entity = await _db.ResourcePermissions
            .FirstOrDefaultAsync(p => p.UserId == userId 
                && p.ResourceType == resourceType 
                && p.ResourceId == resourceId 
                && p.Permission == permission);

        if (permission_entity is not null)
        {
            _db.ResourcePermissions.Remove(permission_entity);
            await _db.SaveChangesAsync();
        }
    }
}
