using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Server.Data;
using System.Security.Claims;

namespace Server.Components.Pages;

public partial class Topics
{
    [Inject] private ApplicationDbContext DbContext { get; set; } = default!;
    [Inject] private IHttpContextAccessor HttpContextAccessor { get; set; } = default!;

    private readonly List<TopicRow> TopicsList = [];
    private string NewTopicName = string.Empty;
    private string NewTopicDescription = string.Empty;
    private string? FeedbackMessage;

    private bool CanCreateTopic => HttpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated == true;

    protected override async Task OnInitializedAsync() => await LoadAsync();

    private async Task LoadAsync()
    {
        FeedbackMessage = null;

        var topics = await DbContext.Topics
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync();

        var boardIds = topics.Select(x => x.BoardId).ToList();
        var postCounts = boardIds.Count == 0
            ? new Dictionary<int, int>()
            : await DbContext.BoardPosts
                .AsNoTracking()
                .Where(x => boardIds.Contains(x.BoardId) && !x.IsDeleted)
                .GroupBy(x => x.BoardId)
                .Select(group => new { group.Key, Count = group.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count);

        var commentCounts = boardIds.Count == 0
            ? new Dictionary<int, int>()
            : await DbContext.BoardPostComments
                .AsNoTracking()
                .Join(
                    DbContext.BoardPosts.AsNoTracking(),
                    comment => comment.PostId,
                    post => post.Id,
                    (comment, post) => new { comment, post })
                .Where(x => boardIds.Contains(x.post.BoardId) && !x.comment.IsDeleted)
                .GroupBy(x => x.post.BoardId)
                .Select(group => new { group.Key, Count = group.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count);

        TopicsList.Clear();
        TopicsList.AddRange(topics.Select(x => new TopicRow(
            x.Id,
            x.Name,
            x.Slug,
            x.Description,
            x.CreatedAtUtc,
            postCounts.GetValueOrDefault(x.BoardId),
            commentCounts.GetValueOrDefault(x.BoardId))));
    }

    private async Task CreateTopicAsync()
    {
        var userId = HttpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            FeedbackMessage = "You must be signed in to create topics.";
            return;
        }

        var name = NewTopicName.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            FeedbackMessage = "Topic name is required.";
            return;
        }

        var slug = BuildSlug(name);
        var exists = await DbContext.Topics.AsNoTracking().AnyAsync(x => x.Slug == slug);
        if (exists)
        {
            FeedbackMessage = "A topic with this name already exists.";
            return;
        }

        var board = new BoardEntity
        {
            Name = name,
            Slug = $"t-{slug}",
            Description = NewTopicDescription.Trim(),
            EntityType = "Topic",
            EntityId = slug,
            CreatedAtUtc = DateTime.UtcNow,
            IsLocked = false
        };

        DbContext.Boards.Add(board);
        await DbContext.SaveChangesAsync();

        DbContext.Topics.Add(new TopicEntity
        {
            Name = name,
            Slug = slug,
            Description = NewTopicDescription.Trim(),
            CreatedByUserId = userId,
            BoardId = board.Id,
            CreatedAtUtc = DateTime.UtcNow
        });

        await DbContext.SaveChangesAsync();

        NewTopicName = string.Empty;
        NewTopicDescription = string.Empty;
        await LoadAsync();
    }

    private static string BuildSlug(string value)
    {
        var chars = value
            .Trim()
            .ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) ? c : '-')
            .ToArray();

        var slug = new string(chars);
        while (slug.Contains("--", StringComparison.Ordinal))
        {
            slug = slug.Replace("--", "-", StringComparison.Ordinal);
        }

        return slug.Trim('-');
    }

    private sealed record TopicRow(
        int Id,
        string Name,
        string Slug,
        string Description,
        DateTime CreatedAtUtc,
        int PostCount,
        int CommentCount);
}
