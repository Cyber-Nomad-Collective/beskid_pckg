using Server.Data;

namespace Server.Services.Notifications;

public interface INotificationActionHandler
{
    bool CanHandle(NotificationType type, string actionName);
    Task<bool> HandleAsync(NotificationEntity notification, string actionName, string? payload, CancellationToken ct = default);
}

public sealed class DefaultNotificationActionHandler : INotificationActionHandler
{
    public bool CanHandle(NotificationType type, string actionName) => false;

    public Task<bool> HandleAsync(NotificationEntity notification, string actionName, string? payload, CancellationToken ct = default)
        => Task.FromResult(false);
}
