using System.Security.Claims;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using Server.Data;

namespace Server.Features.Boards;

public sealed class GetBoardPostCommentsEndpoint : EndpointWithoutRequest<GetBoardPostCommentsResponse>
{
    public ApplicationDbContext Db { get; set; } = default!;

    public override void Configure()
    {
        Get("/boards/posts/{postId}/comments");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var postId = Route<int>("postId");
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        var postExists = await Db.BoardPosts
            .AsNoTracking()
            .AnyAsync(p => p.Id == postId && !p.IsDeleted, ct);

        if (!postExists)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var comments = await Db.BoardPostComments
            .AsNoTracking()
            .Where(c => c.PostId == postId && !c.IsDeleted)
            .OrderBy(c => c.CreatedAtUtc)
            .Select(c => new BoardPostCommentDto(
                c.Id,
                c.PostId,
                c.ParentCommentId,
                c.AuthorUserId,
                c.Content,
                c.CreatedAtUtc,
                c.EditedAtUtc,
                c.UpvoteCount,
                c.DownvoteCount,
                0))
            .ToListAsync(ct);

        if (!string.IsNullOrWhiteSpace(userId) && comments.Count > 0)
        {
            var commentIds = comments.Select(c => c.Id).ToList();
            var userVotes = await Db.BoardCommentVotes
                .AsNoTracking()
                .Where(v => commentIds.Contains(v.CommentId) && v.UserId == userId)
                .ToDictionaryAsync(v => v.CommentId, v => v.VoteValue, ct);

            comments = comments
                .Select(c => c with { CurrentUserVote = userVotes.GetValueOrDefault(c.Id, 0) })
                .ToList();
        }

        await Send.OkAsync(new GetBoardPostCommentsResponse(comments), ct);
    }
}

public sealed record GetBoardPostCommentsResponse(List<BoardPostCommentDto> Comments);

public sealed record BoardPostCommentDto(
    int Id,
    int PostId,
    int? ParentCommentId,
    string AuthorUserId,
    string Content,
    DateTime CreatedAtUtc,
    DateTime? EditedAtUtc,
    int UpvoteCount,
    int DownvoteCount,
    int CurrentUserVote
);
