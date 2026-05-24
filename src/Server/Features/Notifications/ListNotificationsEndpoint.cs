using System.Security.Claims;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using Server.Data;

namespace Server.Features.Notifications;

public sealed class ListNotificationsEndpoint : EndpointWithoutRequest<ListNotificationsResponse>
{
    public ApplicationDbContext Db { get; set; } = default!;

    public override void Configure()
    {
        Get("/users/notifications");
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

        var items = await Db.Notifications
            .AsNoTracking()
            .Where(n => n.UserId == userId)
            .Select(n => new NotificationDto(
                n.Id,
                n.Type,
                n.Title,
                n.Message ?? string.Empty,
                n.IsRead,
                n.CreatedAtUtc))
            .ToListAsync(ct);

        items = items
            .OrderByDescending(n => n.CreatedAtUtc)
            .Take(50)
            .ToList();

        await Send.OkAsync(new ListNotificationsResponse(items), ct);
    }

}

public sealed record NotificationDto(Guid Id, NotificationType Type, string Title, string Message, bool IsRead, DateTimeOffset CreatedAtUtc);
public sealed record ListNotificationsResponse(List<NotificationDto> Items);
