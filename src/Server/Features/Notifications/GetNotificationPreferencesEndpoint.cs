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
            .Select(p => new NotificationPreferenceDto(p.Type, p.Scope, p.ScopeId, p.SendEmail, p.IncludeInSpotlight))
            .ToListAsync(ct);

        var followedPackages = await Db.PackageFollows
            .AsNoTracking()
            .Where(f => f.UserId == userId)
            .Join(
                Db.Packages.AsNoTracking(),
                follow => follow.PackageId,
                package => package.Id.ToString(),
                (follow, package) => new ScopedNotificationTargetDto(package.Id.ToString(), package.Name))
            .OrderBy(x => x.Label)
            .ToListAsync(ct);

        var authoredPostIds = Db.BoardPosts
            .AsNoTracking()
            .Where(p => p.AuthorUserId == userId && !p.IsDeleted)
            .Select(p => p.Id);

        var commentedPostIds = Db.BoardPostComments
            .AsNoTracking()
            .Where(c => c.AuthorUserId == userId && !c.IsDeleted)
            .Select(c => c.PostId);

        var relevantThreadIds = authoredPostIds
            .Union(commentedPostIds);

        var followedThreads = await Db.BoardPosts
            .AsNoTracking()
            .Where(p => relevantThreadIds.Contains(p.Id) && !p.IsDeleted)
            .OrderByDescending(p => p.CreatedAtUtc)
            .Take(25)
            .Select(p => new ScopedNotificationTargetDto(p.Id.ToString(), p.Title))
            .ToListAsync(ct);

        await Send.OkAsync(new GetNotificationPreferencesResponse(prefs, followedPackages, followedThreads), ct);
    }

}

public sealed record NotificationPreferenceDto(NotificationType Type, NotificationPreferenceScope Scope, string ScopeId, bool SendEmail, bool IncludeInSpotlight);
public sealed record ScopedNotificationTargetDto(string ScopeId, string Label);
public sealed record GetNotificationPreferencesResponse(
    List<NotificationPreferenceDto> Items,
    List<ScopedNotificationTargetDto> FollowedPackages,
    List<ScopedNotificationTargetDto> FollowedThreads);
