using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using pckg.Data;

namespace Server.Features.Boards;

public sealed class GetBoardEndpoint : EndpointWithoutRequest<GetBoardResponse>
{
    public ApplicationDbContext Db { get; set; } = default!;

    public override void Configure()
    {
        Get("/boards/{slug}");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var slug = Route<string>("slug");
        if (string.IsNullOrWhiteSpace(slug))
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var board = await Db.Boards
            .FirstOrDefaultAsync(b => b.Slug == slug, ct);

        if (board is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(new GetBoardResponse(
            board.Id,
            board.Name,
            board.Slug,
            board.Description,
            board.EntityType,
            board.EntityId,
            board.IsLocked
        ), ct);
    }
}

public sealed record GetBoardResponse(
    int Id,
    string Name,
    string Slug,
    string Description,
    string EntityType,
    string? EntityId,
    bool IsLocked
);
