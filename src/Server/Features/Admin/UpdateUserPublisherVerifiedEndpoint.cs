using FastEndpoints;
using Microsoft.AspNetCore.Identity;
using Server.Data;

namespace Server.Features.Admin;

public sealed class UpdateUserPublisherVerifiedEndpoint(UserManager<ApplicationUser> userManager)
    : Endpoint<UpdatePublisherVerifiedRequest, UpdatePublisherVerifiedResponse>
{
    public override void Configure()
    {
        Put("/admin/users/{userId}/publisher-verified");
        Roles("SuperAdmin");
    }

    public override async Task HandleAsync(UpdatePublisherVerifiedRequest req, CancellationToken ct)
    {
        var userId = Route<string>("userId");
        if (string.IsNullOrWhiteSpace(userId))
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        user.IsPublisherVerified = req.IsPublisherVerified;
        var update = await userManager.UpdateAsync(user);
        if (!update.Succeeded)
        {
            var message = string.Join(' ', update.Errors.Select(e => e.Description));
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await Send.OkAsync(new UpdatePublisherVerifiedResponse(false, message), ct);
            return;
        }

        await Send.OkAsync(new UpdatePublisherVerifiedResponse(true, "Publisher verification updated."), ct);
    }
}
