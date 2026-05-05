namespace Server.Components.Pages.Dashboard.Admin;

internal sealed record GetEmailSettingsResponse(
    string? SmtpHost,
    int SmtpPort,
    bool EnableSsl,
    string? Username,
    string? Password,
    string FromEmail,
    string FromName);

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
