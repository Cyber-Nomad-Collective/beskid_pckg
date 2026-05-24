using System.Security.Claims;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using Server.Data;
using Server.Services.Notifications;

namespace Server.Features.Notifications;

public sealed class ExecuteNotificationActionEndpoint : Endpoint<ExecuteNotificationActionRequest>
{
    public ApplicationDbContext Db { get; set; } = default!;
    public INotificationActionHandler ActionHandler { get; set; } = default!;

    public override void Configure()
    {
        Post("/users/notifications/action");
        Roles("User", "SuperAdmin");
    }

    public override async Task HandleAsync(ExecuteNotificationActionRequest req, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        var notif = await Db.Notifications.AsNoTracking().FirstOrDefaultAsync(n => n.Id == req.NotificationId && n.UserId == userId, ct);
        if (notif is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        if (!ActionHandler.CanHandle(notif.Type, req.ActionName))
        {
            await Send.OkAsync(new { handled = false }, ct);
            return;
        }

        var handled = await ActionHandler.HandleAsync(notif, req.ActionName, req.Payload, ct);
        await Send.OkAsync(new { handled }, ct);
    }
}

public sealed class ExecuteNotificationActionRequest
{
    public Guid NotificationId { get; set; }
    public string ActionName { get; set; } = string.Empty;
    public string? Payload { get; set; }
}
