using System.Security.Claims;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using Server.Data;

namespace Server.Features.Admin;

public sealed class ListBlockedLinksEndpoint(ApplicationDbContext db)
    : EndpointWithoutRequest<List<BlockedLinkPatternDto>>
{
    public override void Configure()
    {
        Get("/admin/blocked-links");
        Roles("SuperAdmin");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var rows = await db.BlockedLinkPatterns
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new BlockedLinkPatternDto(x.Id, x.Pattern, x.Note, x.CreatedAtUtc))
            .ToListAsync(ct);
        await Send.OkAsync(rows, ct);
    }
}

public sealed class AddBlockedLinkEndpoint(ApplicationDbContext db)
    : Endpoint<AddBlockedLinkRequest, AddBlockedLinkResponse>
{
    public override void Configure()
    {
        Post("/admin/blocked-links");
        Roles("SuperAdmin");
    }

    public override async Task HandleAsync(AddBlockedLinkRequest req, CancellationToken ct)
    {
        var pattern = req.Pattern?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(pattern))
        {
            await Send.ResponseAsync(new AddBlockedLinkResponse(false, "Pattern is required.", null), StatusCodes.Status400BadRequest, ct);
            return;
        }

        if (pattern.Length > 512)
        {
            await Send.ResponseAsync(new AddBlockedLinkResponse(false, "Pattern is too long.", null), StatusCodes.Status400BadRequest, ct);
            return;
        }

        var exists = await db.BlockedLinkPatterns.AnyAsync(x => x.Pattern == pattern, ct);
        if (exists)
        {
            await Send.ResponseAsync(new AddBlockedLinkResponse(false, "That pattern is already blocked.", null), StatusCodes.Status409Conflict, ct);
            return;
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "system";
        var entity = new BlockedLinkPatternEntity
        {
            Id = Guid.NewGuid(),
            Pattern = pattern,
            Note = string.IsNullOrWhiteSpace(req.Note) ? null : req.Note.Trim(),
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };
        db.BlockedLinkPatterns.Add(entity);
        await db.SaveChangesAsync(ct);

        await Send.OkAsync(new AddBlockedLinkResponse(true, "Pattern added.", new BlockedLinkPatternDto(entity.Id, entity.Pattern, entity.Note, entity.CreatedAtUtc)), ct);
    }
}

public sealed class DeleteBlockedLinkEndpoint(ApplicationDbContext db)
    : EndpointWithoutRequest
{
    public override void Configure()
    {
        Delete("/admin/blocked-links/{id:guid}");
        Roles("SuperAdmin");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<Guid>("id");
        var row = await db.BlockedLinkPatterns.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (row is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        db.BlockedLinkPatterns.Remove(row);
        await db.SaveChangesAsync(ct);
        await Send.NoContentAsync(ct);
    }
}

public sealed record BlockedLinkPatternDto(Guid Id, string Pattern, string? Note, DateTimeOffset CreatedAtUtc);
public sealed record AddBlockedLinkRequest(string Pattern, string? Note);
public sealed record AddBlockedLinkResponse(bool Success, string Message, BlockedLinkPatternDto? Item);
