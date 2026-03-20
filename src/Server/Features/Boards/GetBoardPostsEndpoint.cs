using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Server.Data;

namespace Server.Features.Boards;

public sealed class GetBoardPostsEndpoint : EndpointWithoutRequest<GetBoardPostsResponse>
{
    public ApplicationDbContext Db { get; set; } = default!;

    public override void Configure()
    {
        Get("/boards/{boardId}/posts");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var boardId = Route<int>("boardId");
        var page = Query<int>("page", isRequired: false);
        var pageSize = Query<int>("pageSize", isRequired: false);
        var postTypeQuery = Query<string>("postType", isRequired: false);
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        BoardPostType? postTypeFilter = null;

        if (!string.IsNullOrWhiteSpace(postTypeQuery) && Enum.TryParse<BoardPostType>(postTypeQuery, true, out var parsedType))
        {
            postTypeFilter = parsedType;
        }

        if (page <= 0) page = 1;
        if (pageSize <= 0 || pageSize > 100) pageSize = 20;

        var skip = (page - 1) * pageSize;

        var postsBaseQuery = Db.BoardPosts
            .Where(p => p.BoardId == boardId && !p.IsDeleted);

        if (postTypeFilter is not null)
        {
            postsBaseQuery = postsBaseQuery.Where(p => p.PostType == postTypeFilter.Value);
        }

        var postsQuery = postsBaseQuery
            .OrderByDescending(p => p.IsPinned)
            .ThenByDescending(p => p.CreatedAtUtc)
            .Skip(skip)
            .Take(pageSize);

        var posts = await postsQuery
            .Select(p => new BoardPostDto(
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
                0 // CurrentUserVote placeholder
            ))
            .ToListAsync(ct);

        // Get current user's votes for these posts if authenticated
        if (!string.IsNullOrWhiteSpace(userId))
        {
            var postIds = posts.Select(p => p.Id).ToList();
            var userVotes = await Db.BoardPostVotes
                .Where(v => postIds.Contains(v.PostId) && v.UserId == userId)
                .ToDictionaryAsync(v => v.PostId, v => v.VoteValue, ct);

            posts = posts.Select(p => p with { CurrentUserVote = userVotes.GetValueOrDefault(p.Id, 0) }).ToList();
        }

        var totalCount = await postsBaseQuery.CountAsync(ct);

        await Send.OkAsync(new GetBoardPostsResponse(posts, totalCount, page, pageSize), ct);
    }
}

public sealed record GetBoardPostsResponse(
    List<BoardPostDto> Posts,
    int TotalCount,
    int Page,
    int PageSize
);

public sealed record BoardPostDto(
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
    int CurrentUserVote
);
