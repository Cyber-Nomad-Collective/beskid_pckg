using Microsoft.AspNetCore.SignalR;
using Server.Data;
using Server.Hubs;

namespace Server.Services.Notifications;

public sealed class SignalRNotificationBroadcaster(IHubContext<NotificationsHub> hubContext)
    : INotificationBroadcaster
{
    public async Task BroadcastAsync(NotificationEntity notification, CancellationToken ct = default)
    {
        var group = $"user:{notification.UserId}";
        await hubContext.Clients.Group(group).SendAsync(
            "notificationReceived",
            new
            {
                id = notification.Id.ToString(),
                type = (int)notification.Type,
                title = notification.Title,
                message = notification.Message,
                createdAtUtc = notification.CreatedAtUtc
            },
            ct);
    }
}
