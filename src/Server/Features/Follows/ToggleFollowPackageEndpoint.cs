using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using Server.Data;
using System.Security.Claims;

namespace Server.Features.Follows;

public sealed class ToggleFollowPackageEndpoint : Endpoint<ToggleFollowPackageEndpoint.Request, ToggleFollowPackageEndpoint.Response>
{
    public ApplicationDbContext Db { get; set; } = default!;

    public sealed record Request(string PackageId);
    public sealed record Response(bool IsFollowing);

    public override void Configure()
    {
        Post("/users/follows/packages/toggle");
        Roles("User", "SuperAdmin");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(req.PackageId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        var exists = await Db.PackageFollows
            .FirstOrDefaultAsync(x => x.UserId == userId && x.PackageId == req.PackageId, ct);

        if (exists is null)
        {
            Db.PackageFollows.Add(new FollowPackageEntity
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                PackageId = req.PackageId,
                CreatedAtUtc = DateTimeOffset.UtcNow
            });
            await Db.SaveChangesAsync(ct);
            await Send.OkAsync(new Response(true), ct);
            return;
        }
        else
        {
            Db.PackageFollows.Remove(exists);
            await Db.SaveChangesAsync(ct);
            await Send.OkAsync(new Response(false), ct);
        }
    }
}
