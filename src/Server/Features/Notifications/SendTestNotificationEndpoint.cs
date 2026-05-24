using System.Security.Claims;
using FastEndpoints;
using Server.Data;
using Server.Services.Notifications;

namespace Server.Features.Notifications;

public sealed class SendTestNotificationEndpoint : EndpointWithoutRequest
{
    public INotificationService Notifications { get; set; } = default!;

    public override void Configure()
    {
        Post("/users/notifications/test");
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

        await Notifications.PublishAsync(userId, NotificationType.System, "This is a test notification", "It works!", ct: ct);
        await Send.OkAsync(new { ok = true }, ct);
    }
}
