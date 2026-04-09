using Server.Data;

namespace Server.Services.Notifications;

public interface INotificationService
{
    Task<NotificationEntity> PublishAsync(
        string userId,
        NotificationType type,
        string title,
        string? message = null,
        string? dataJson = null,
        NotificationPreferenceScope preferenceScope = NotificationPreferenceScope.None,
        string? preferenceScopeId = null,
        CancellationToken ct = default);
    Task MarkReadAsync(string userId, Guid notificationId, CancellationToken ct = default);
}
