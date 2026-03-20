using Microsoft.EntityFrameworkCore;
using Server.Data;

namespace Server.Services.Notifications;

public sealed class NotificationService(
    ApplicationDbContext db,
    INotificationBroadcaster broadcaster,
    Email.IEmailSender emailSender)
    : INotificationService
{
    public async Task<NotificationEntity> PublishAsync(
        string userId,
        NotificationType type,
        string title,
        string? message = null,
        string? dataJson = null,
        CancellationToken ct = default)
    {
        var notification = new NotificationEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Type = type,
            Title = title,
            Message = message,
            DataJson = dataJson,
            IsRead = false,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        await db.Notifications.AddAsync(notification, ct);
        await db.SaveChangesAsync(ct);

        // Broadcast to connected clients
        await broadcaster.BroadcastAsync(notification, ct);

        // Check email preferences and send email if enabled
        var pref = await db.NotificationPreferences
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == userId && p.Type == type, ct);

        if (pref?.SendEmail == true)
        {
            try
            {
                await emailSender.SendAsync(userId, title, message ?? string.Empty, ct);
            }
            catch
            {
                // swallow email errors for now; consider logging in the future
            }
        }

        return notification;
    }

    public async Task MarkReadAsync(string userId, Guid notificationId, CancellationToken ct = default)
    {
        var notif = await db.Notifications.FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId, ct);
        if (notif is null) return;
        if (!notif.IsRead)
        {
            notif.IsRead = true;
            await db.SaveChangesAsync(ct);
        }
    }
}
