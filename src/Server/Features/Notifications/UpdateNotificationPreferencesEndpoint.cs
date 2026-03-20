using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using Server.Data;
using System.Security.Claims;

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
            var existing = await Db.NotificationPreferences
                .FirstOrDefaultAsync(p => p.UserId == userId && p.Type == item.Type, ct);
            if (existing is null)
            {
                existing = new UserNotificationPreferenceEntity
                {
                    UserId = userId,
                    Type = item.Type,
                    SendEmail = item.SendEmail,
                    IncludeInSpotlight = item.IncludeInSpotlight
                };
                await Db.NotificationPreferences.AddAsync(existing, ct);
            }
            else
            {
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
    public bool SendEmail { get; set; }
    public bool IncludeInSpotlight { get; set; }
}
