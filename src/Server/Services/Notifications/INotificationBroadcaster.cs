using Server.Data;

namespace Server.Services.Notifications;

public interface INotificationBroadcaster
{
    Task BroadcastAsync(NotificationEntity notification, CancellationToken ct = default);
}
