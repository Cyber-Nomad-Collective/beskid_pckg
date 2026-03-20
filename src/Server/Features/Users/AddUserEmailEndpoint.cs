using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Server.Data;

namespace Server.Features.Users;

public sealed class AddUserEmailEndpoint : Endpoint<AddUserEmailRequest, AddUserEmailResponse>
{
    public ApplicationDbContext Db { get; set; } = default!;

    public override void Configure()
    {
        Post("/users/emails");
        Roles("User", "SuperAdmin");
    }

    public override async Task HandleAsync(AddUserEmailRequest req, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        var emailExists = await Db.UserEmails
            .AnyAsync(e => e.UserId == userId && e.Email == req.Email, ct);

        if (emailExists)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await Send.OkAsync(new AddUserEmailResponse(false, "This email is already added."), ct);
            return;
        }

        var newEmail = new UserEmailEntity
        {
            UserId = userId,
            Email = req.Email,
            IsVerified = false,
            IsPrimary = false,
            AddedAtUtc = DateTime.UtcNow
        };

        Db.UserEmails.Add(newEmail);
        await Db.SaveChangesAsync(ct);

        await Send.OkAsync(new AddUserEmailResponse(true, "Email added successfully."), ct);
    }
}

public sealed record AddUserEmailRequest(string Email);
public sealed record AddUserEmailResponse(bool Success, string Message);
