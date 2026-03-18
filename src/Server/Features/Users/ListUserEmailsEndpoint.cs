using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using pckg.Data;
using System.Security.Claims;

namespace Server.Features.Users;

public sealed class ListUserEmailsEndpoint : EndpointWithoutRequest<ListUserEmailsResponse>
{
    public ApplicationDbContext Db { get; set; } = default!;

    public override void Configure()
    {
        Get("/users/emails");
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

        var emails = await Db.UserEmails
            .Where(e => e.UserId == userId)
            .OrderByDescending(e => e.IsPrimary)
            .ThenBy(e => e.AddedAtUtc)
            .Select(e => new UserEmailDto(
                e.Id,
                e.Email,
                e.IsVerified,
                e.IsPrimary,
                e.AddedAtUtc
            ))
            .ToListAsync(ct);

        await Send.OkAsync(new ListUserEmailsResponse(emails), ct);
    }
}

public sealed record ListUserEmailsResponse(List<UserEmailDto> Emails);

public sealed record UserEmailDto(
    int Id,
    string Email,
    bool IsVerified,
    bool IsPrimary,
    DateTime AddedAtUtc
);
