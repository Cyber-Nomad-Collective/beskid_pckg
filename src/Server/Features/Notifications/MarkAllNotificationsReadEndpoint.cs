using System.Security.Claims;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using Server.Data;

namespace Server.Features.Notifications;

public sealed class MarkAllNotificationsReadEndpoint : EndpointWithoutRequest
{
    public ApplicationDbContext Db { get; set; } = default!;

    public override void Configure()
    {
        Post("/users/notifications/mark-all-read");
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

        await Db.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(setters => setters.SetProperty(n => n.IsRead, true), ct);

        await Send.OkAsync(new { ok = true }, ct);
    }
}
