using System.Security.Claims;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using Server.Data;
using Server.Services.Notifications;

namespace Server.Features.Notifications;

/// <summary>React projections over the canonical notification records and read-state service.</summary>
public sealed class ListReactCommunityNotificationsEndpoint(ApplicationDbContext db)
    : EndpointWithoutRequest<List<ReactCommunityNotification>>
{
    public override void Configure()
    {
        Get("/community/notifications");
        Options(options => options.RequireAuthorization());
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        var notifications = await db.Notifications.AsNoTracking()
            .Where(notification => notification.UserId == userId)
            .OrderByDescending(notification => notification.CreatedAtUtc)
            .Take(50)
            .Select(notification => new ReactCommunityNotification(
                notification.Id,
                notification.UserId,
                notification.Type.ToString(),
                notification.Title,
                null,
                null,
                notification.IsRead))
            .ToListAsync(ct);
        await Send.OkAsync(notifications, ct);
    }
}

public sealed class MarkReactCommunityNotificationReadEndpoint(INotificationService notifications)
    : EndpointWithoutRequest
{
    public override void Configure()
    {
        Post("/community/notifications/{notificationId:guid}/read");
        Options(options => options.RequireAuthorization());
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        await notifications.MarkReadAsync(userId, Route<Guid>("notificationId"), ct);
        await Send.NoContentAsync(ct);
    }
}

public sealed class UpdateReactCommunityNotificationPreferenceEndpoint(ApplicationDbContext db)
    : Endpoint<ReactCommunityNotificationPreferenceRequest>
{
    public override void Configure()
    {
        Put("/community/notification-preferences");
        Options(options => options.RequireAuthorization());
    }

    public override async Task HandleAsync(ReactCommunityNotificationPreferenceRequest req, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        // The canonical store models delivery preference per notification type. The compact
        // React switch only controls email delivery, leaving in-app notifications intact.
        var sendEmail = req.Mode switch
        {
            "all" => true,
            "mentionsOnly" => false,
            _ => (bool?)null
        };
        if (sendEmail is null)
        {
            AddError("mode", "Mode must be 'all' or 'mentionsOnly'.");
            await Send.ErrorsAsync(StatusCodes.Status400BadRequest, ct);
            return;
        }

        var preferenceTypes = Enum.GetValues<NotificationType>().Where(type => type != NotificationType.Unknown).ToArray();
        var existing = await db.NotificationPreferences
            .Where(preference => preference.UserId == userId && preference.Scope == NotificationPreferenceScope.None && preference.ScopeId == string.Empty)
            .ToListAsync(ct);
        foreach (var type in preferenceTypes)
        {
            var preference = existing.SingleOrDefault(item => item.Type == type);
            if (preference is null)
            {
                db.NotificationPreferences.Add(new UserNotificationPreferenceEntity
                {
                    UserId = userId,
                    Type = type,
                    Scope = NotificationPreferenceScope.None,
                    ScopeId = string.Empty,
                    SendEmail = sendEmail.Value,
                    IncludeInSpotlight = false
                });
            }
            else
            {
                preference.SendEmail = sendEmail.Value;
            }
        }

        await db.SaveChangesAsync(ct);
        await Send.NoContentAsync(ct);
    }
}

public sealed record ReactCommunityNotification(Guid Id, string Recipient, string Scope, string Actor, int? PostId, int? CommentId, bool IsRead);
public sealed record ReactCommunityNotificationPreferenceRequest(string Mode);
