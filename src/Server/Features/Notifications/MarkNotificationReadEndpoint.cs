using System.Security.Claims;
using FastEndpoints;
using Server.Services.Notifications;

namespace Server.Features.Notifications;

public sealed class MarkNotificationReadEndpoint : Endpoint<MarkReadRequest>
{
    public INotificationService Notifications { get; set; } = default!;

    public override void Configure()
    {
        Post("/users/notifications/mark-read");
        Roles("User", "SuperAdmin");
    }

    public override async Task HandleAsync(MarkReadRequest req, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        await Notifications.MarkReadAsync(userId, req.NotificationId, ct);
        await Send.OkAsync(new { ok = true }, ct);
    }

}

public sealed class MarkReadRequest
{
    public Guid NotificationId { get; set; }
}
