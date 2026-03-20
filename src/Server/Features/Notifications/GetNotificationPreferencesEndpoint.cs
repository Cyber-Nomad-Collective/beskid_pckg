using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using Server.Data;
using System.Security.Claims;

namespace Server.Features.Notifications;

public sealed class GetNotificationPreferencesEndpoint : EndpointWithoutRequest<GetNotificationPreferencesResponse>
{
    public ApplicationDbContext Db { get; set; } = default!;

    public override void Configure()
    {
        Get("/users/notification-preferences");
        Roles("User", "SuperAdmin");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        var prefs = await Db.NotificationPreferences
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .Select(p => new NotificationPreferenceDto(p.Type, p.SendEmail, p.IncludeInSpotlight))
            .ToListAsync(ct);

        await Send.OkAsync(new GetNotificationPreferencesResponse(prefs), ct);
    }

}

public sealed record NotificationPreferenceDto(NotificationType Type, bool SendEmail, bool IncludeInSpotlight);
public sealed record GetNotificationPreferencesResponse(List<NotificationPreferenceDto> Items);
