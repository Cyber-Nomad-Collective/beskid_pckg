using System.Security.Claims;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using Server.Data;

namespace Server.Features.Follows;

public sealed class ToggleFollowPublisherEndpoint : Endpoint<ToggleFollowPublisherEndpoint.Request, ToggleFollowPublisherEndpoint.Response>
{
    public ApplicationDbContext Db { get; set; } = default!;

    public sealed record Request(string PublisherUserId);
    public new sealed record Response(bool IsFollowing);

    public override void Configure()
    {
        Post("/users/follows/publishers/toggle");
        Roles("User", "SuperAdmin");
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(req.PublisherUserId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        if (string.Equals(userId, req.PublisherUserId, StringComparison.Ordinal))
        {
            await Send.OkAsync(new Response(true), ct);
            return;
        }

        var exists = await Db.PublisherFollows
            .FirstOrDefaultAsync(x => x.UserId == userId && x.PublisherUserId == req.PublisherUserId, ct);

        if (exists is null)
        {
            Db.PublisherFollows.Add(new FollowPublisherEntity
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                PublisherUserId = req.PublisherUserId,
                CreatedAtUtc = DateTimeOffset.UtcNow
            });
            await Db.SaveChangesAsync(ct);
            await Send.OkAsync(new Response(true), ct);
            return;
        }
        else
        {
            Db.PublisherFollows.Remove(exists);
            await Db.SaveChangesAsync(ct);
            await Send.OkAsync(new Response(false), ct);
        }
    }
}
