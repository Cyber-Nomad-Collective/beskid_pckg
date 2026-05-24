using Server.Data;
using Wolverine;
using Wolverine.SignalR;

namespace Server.Services.Notifications;

public sealed class WolverineNotificationBroadcaster(IMessageBus bus) : INotificationBroadcaster
{
    public async Task BroadcastAsync(NotificationEntity notification, CancellationToken ct = default)
    {
        var msg = new NotificationPushed(
            notification.Id.ToString(),
            (int)notification.Type,
            notification.Title,
            notification.Message,
            notification.CreatedAtUtc);

        var group = $"user:{notification.UserId}";

        // Restrict the message to the user's SignalR group
        var envelope = msg.ToWebSocketGroup(group);
        await bus.PublishAsync(envelope);
    }
}
