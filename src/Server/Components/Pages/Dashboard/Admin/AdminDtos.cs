namespace Server.Components.Pages.Dashboard.Admin;

internal sealed record GetEmailSettingsResponse(
    string? SmtpHost,
    int SmtpPort,
    bool EnableSsl,
    string? Username,
    string? Password,
    string FromEmail,
    string FromName);

internal sealed record ListUsersResponse(List<UserDto> Users, int TotalCount, int Page, int PageSize);

internal sealed record UserDto(
    string Id,
    string Email,
    string DisplayName,
    bool EmailConfirmed,
    List<string> Roles,
    double Rating);

internal sealed record UpdateUserRolesRequest(List<string> Roles);

internal sealed record UpdateUserRolesResponse(bool Success, string Message);

internal sealed record BlockedLinkRowDto(Guid Id, string Pattern, string? Note, DateTimeOffset CreatedAtUtc);

internal sealed record AddBlockedLinkApiRequest(string Pattern, string? Note);

internal sealed record AddBlockedLinkApiResponse(bool Success, string Message, BlockedLinkRowDto? Item);

internal sealed record RegistryActivityRowDto(
    DateTimeOffset TimestampUtc,
    string Severity,
    string Action,
    string Message,
    string? TraceId,
    string? UserId,
    string? PackageName,
    string? Version);
