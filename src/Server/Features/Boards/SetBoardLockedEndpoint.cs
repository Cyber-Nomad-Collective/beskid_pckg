using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using Server.Data;
using Server.Services;
using System.Security.Claims;

namespace Server.Features.Boards;

public sealed class SetBoardLockedEndpoint(
    ApplicationDbContext db,
    IAuthorizationService authorization)
    : Endpoint<SetBoardLockedRequest, SetBoardLockedResponse>
{
    public override void Configure()
    {
        Post("/boards/{boardId}/moderation/lock");
        Options(x => x.RequireAuthorization());
        Roles("User", "SuperAdmin", "Moderator");
    }

    public override async Task HandleAsync(SetBoardLockedRequest req, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        var boardId = Route<int>("boardId");
        var board = await db.Boards.FirstOrDefaultAsync(b => b.Id == boardId, ct);
        if (board is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        if (!await authorization.CanModerateBoardAsync(userId, boardId))
        {
            await Send.ResponseAsync(new SetBoardLockedResponse(false, "You cannot change lock state for this board."), StatusCodes.Status403Forbidden, ct);
            return;
        }

        board.IsLocked = req.Locked;
        await db.SaveChangesAsync(ct);

        await Send.OkAsync(new SetBoardLockedResponse(true, req.Locked ? "Board locked." : "Board unlocked."), ct);
    }
}

public sealed record SetBoardLockedRequest(bool Locked);
public sealed record SetBoardLockedResponse(bool Success, string Message);
