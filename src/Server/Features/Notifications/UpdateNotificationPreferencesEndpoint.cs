using System.Security.Claims;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using Server.Data;

namespace Server.Features.Notifications;

public sealed class UpdateNotificationPreferencesEndpoint : Endpoint<UpdateNotificationPreferencesRequest>
{
    public ApplicationDbContext Db { get; set; } = default!;

    public override void Configure()
    {
        Post("/users/notification-preferences");
        Roles("User", "SuperAdmin");
    }

    public override async Task HandleAsync(UpdateNotificationPreferencesRequest req, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        foreach (var item in req.Items)
        {
            var normalizedScopeId = item.Scope == NotificationPreferenceScope.None
                ? string.Empty
                : (item.ScopeId ?? string.Empty).Trim();

            var existing = await Db.NotificationPreferences
                .FirstOrDefaultAsync(p =>
                    p.UserId == userId
                    && p.Type == item.Type
                    && p.Scope == item.Scope
                    && p.ScopeId == normalizedScopeId,
                    ct);
            if (existing is null)
            {
                existing = new UserNotificationPreferenceEntity
                {
                    UserId = userId,
                    Type = item.Type,
                    Scope = item.Scope,
                    ScopeId = normalizedScopeId,
                    SendEmail = item.SendEmail,
                    IncludeInSpotlight = item.IncludeInSpotlight
                };
                await Db.NotificationPreferences.AddAsync(existing, ct);
            }
            else
            {
                existing.Scope = item.Scope;
                existing.ScopeId = normalizedScopeId;
                existing.SendEmail = item.SendEmail;
                existing.IncludeInSpotlight = item.IncludeInSpotlight;
            }
        }

        await Db.SaveChangesAsync(ct);
        await Send.OkAsync(new { ok = true }, ct);
    }

}

public sealed class UpdateNotificationPreferencesRequest
{
    public List<PreferenceItem> Items { get; set; } = new();
}

public sealed class PreferenceItem
{
    public NotificationType Type { get; set; }
    public NotificationPreferenceScope Scope { get; set; }
    public string ScopeId { get; set; } = string.Empty;
    public bool SendEmail { get; set; }
    public bool IncludeInSpotlight { get; set; }
}
