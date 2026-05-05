namespace Server.Features.Admin;

public sealed record ListUsersResponse(
    List<UserDto> Users,
    int TotalCount,
    int Page,
    int PageSize);

public sealed record UserDto(
    string Id,
    string Email,
    string DisplayName,
    bool EmailConfirmed,
    bool IsPublisherVerified,
    List<string> Roles,
    double Rating);

public sealed record UpdateUserRolesRequest(List<string> Roles);

public sealed record UpdateUserRolesResponse(bool Success, string Message);

public sealed record UpdatePublisherVerifiedRequest(bool IsPublisherVerified);

public sealed record UpdatePublisherVerifiedResponse(bool Success, string Message);

public sealed record CreateAdminUserRequest(
    string Email,
    string Password,
    string DisplayName,
    IReadOnlyList<string>? Roles,
    bool EmailConfirmed);

public sealed record CreateAdminUserResponse(bool Success, string Message, string? UserId);
