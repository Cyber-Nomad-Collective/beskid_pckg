namespace Server.Services;

public interface IAuthorizationService
{
    Task<bool> CanModerateAsync(string userId, string resourceType, string resourceId);
    Task<bool> HasPermissionAsync(string userId, string resourceType, string resourceId, string permission);
    Task GrantPermissionAsync(string userId, string resourceType, string resourceId, string permission, string grantedByUserId);
    Task RevokePermissionAsync(string userId, string resourceType, string resourceId, string permission);
    Task<bool> IsSuperAdminAsync(string userId);
    Task<bool> IsGlobalModeratorAsync(string userId);
    Task<bool> IsPackageOwnerAsync(string userId, Guid packageId);
    Task<bool> CanModeratePackageAsync(string userId, Guid packageId);
    Task<bool> CanModerateBoardAsync(string userId, int boardId);
}
