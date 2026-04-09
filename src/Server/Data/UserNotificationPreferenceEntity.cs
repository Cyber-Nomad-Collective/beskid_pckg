namespace Server.Data;

public sealed class UserNotificationPreferenceEntity
{
    public int Id { get; set; }
    public required string UserId { get; set; }
    public NotificationType Type { get; set; }
    public NotificationPreferenceScope Scope { get; set; }
    public string ScopeId { get; set; } = string.Empty;
    public bool SendEmail { get; set; }
    public bool IncludeInSpotlight { get; set; }
}
