using System.Security.Claims;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using Server.Data;

namespace Server.Features.Boards;

public sealed class GetBoardPostEndpoint : EndpointWithoutRequest<GetBoardPostResponse>
{
    public ApplicationDbContext Db { get; set; } = default!;

    public override void Configure()
    {
        Get("/boards/posts/{postId}");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var postId = Route<int>("postId");
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        var post = await Db.BoardPosts
            .AsNoTracking()
            .Where(p => p.Id == postId && !p.IsDeleted)
            .Select(p => new GetBoardPostResponse(
                p.Id,
                p.BoardId,
                p.AuthorUserId,
                p.Title,
                p.Content,
                p.PostType,
                p.CreatedAtUtc,
                p.EditedAtUtc,
                p.UpvoteCount,
                p.DownvoteCount,
                p.IsPinned,
                p.IsLocked,
                0,
                p.Board!.Name,
                p.Board.Slug,
                p.Board.EntityType,
                p.Board.EntityId))
            .FirstOrDefaultAsync(ct);

        if (post is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        if (!string.IsNullOrWhiteSpace(userId))
        {
            var vote = await Db.BoardPostVotes
                .AsNoTracking()
                .Where(v => v.PostId == postId && v.UserId == userId)
                .Select(v => v.VoteValue)
                .FirstOrDefaultAsync(ct);

            post = post with { CurrentUserVote = vote };
        }

        await Send.OkAsync(post, ct);
    }
}

public sealed record GetBoardPostResponse(
    int Id,
    int BoardId,
    string AuthorUserId,
    string Title,
    string Content,
    BoardPostType PostType,
    DateTime CreatedAtUtc,
    DateTime? EditedAtUtc,
    int UpvoteCount,
    int DownvoteCount,
    bool IsPinned,
    bool IsLocked,
    int CurrentUserVote,
    string BoardName,
    string BoardSlug,
    string BoardEntityType,
    string? BoardEntityId
);
