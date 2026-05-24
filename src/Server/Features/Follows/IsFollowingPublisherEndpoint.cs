using System.Security.Claims;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using Server.Data;

namespace Server.Features.Follows;

public sealed class IsFollowingPublisherEndpoint : EndpointWithoutRequest<IsFollowingPublisherEndpoint.Response>
{
    public ApplicationDbContext Db { get; set; } = default!;

    public new sealed record Response(bool IsFollowing);

    public override void Configure()
    {
        Get("/users/follows/publishers/is-following");
        Roles("User", "SuperAdmin");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var publisherUserId = Query<string>("publisherUserId");
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(publisherUserId))
        {
            await Send.OkAsync(new Response(false), ct);
            return;
        }

        if (string.Equals(userId, publisherUserId, StringComparison.Ordinal))
        {
            await Send.OkAsync(new Response(true), ct);
            return;
        }

        var isFollowing = await Db.PublisherFollows
            .AsNoTracking()
            .AnyAsync(x => x.UserId == userId && x.PublisherUserId == publisherUserId, ct);

        await Send.OkAsync(new Response(isFollowing), ct);
    }
}
