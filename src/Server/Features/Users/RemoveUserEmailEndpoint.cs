using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using pckg.Data;
using System.Security.Claims;

namespace Server.Features.Users;

public sealed class RemoveUserEmailEndpoint : EndpointWithoutRequest<RemoveUserEmailResponse>
{
    public ApplicationDbContext Db { get; set; } = default!;

    public override void Configure()
    {
        Delete("/users/emails/{emailId}");
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

        var emailId = Route<int>("emailId");
        var email = await Db.UserEmails
            .FirstOrDefaultAsync(e => e.Id == emailId && e.UserId == userId, ct);

        if (email is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        if (email.IsPrimary)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await Send.OkAsync(new RemoveUserEmailResponse(false, "Cannot remove primary email."), ct);
            return;
        }

        Db.UserEmails.Remove(email);
        await Db.SaveChangesAsync(ct);

        await Send.OkAsync(new RemoveUserEmailResponse(true, "Email removed successfully."), ct);
    }
}

public sealed record RemoveUserEmailResponse(bool Success, string Message);
