namespace Server.Services.Notifications;

public sealed record NotificationPushed(
    string Id,
    int Type,
    string Title,
    string? Message,
    DateTimeOffset CreatedAtUtc
) ;
