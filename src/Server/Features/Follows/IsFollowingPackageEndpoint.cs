using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using Server.Data;
using System.Security.Claims;

namespace Server.Features.Follows;

public sealed class IsFollowingPackageEndpoint : EndpointWithoutRequest<IsFollowingPackageEndpoint.Response>
{
    public ApplicationDbContext Db { get; set; } = default!;

    public new sealed record Response(bool IsFollowing);

    public override void Configure()
    {
        Get("/users/follows/packages/is-following");
        Roles("User", "SuperAdmin");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var packageId = Query<string>("packageId");
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(packageId))
        {
            await Send.OkAsync(new Response(false), ct);
            return;
        }

        var isFollowing = await Db.PackageFollows
            .AsNoTracking()
            .AnyAsync(x => x.UserId == userId && x.PackageId == packageId, ct);

        await Send.OkAsync(new Response(isFollowing), ct);
    }
}
