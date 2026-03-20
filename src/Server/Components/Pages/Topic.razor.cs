using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Server.Data;

namespace Server.Components.Pages;

public partial class Topic
{
    [Inject] private ApplicationDbContext DbContext { get; set; } = default!;

    [Parameter] public string Slug { get; set; } = string.Empty;

    private bool IsLoading = true;
    private string? ErrorMessage;
    private int BoardId;
    private bool IsBoardLocked;
    private string TopicName = string.Empty;
    private string TopicDescription = string.Empty;
    private DateTime CreatedAtUtc;
    private int PostCount;
    private int CommentCount;

    protected override async Task OnParametersSetAsync()
    {
        IsLoading = true;
        ErrorMessage = null;

        try
        {
            if (string.IsNullOrWhiteSpace(Slug))
            {
                ErrorMessage = "Topic not found.";
                return;
            }

            var topic = await DbContext.Topics
                .AsNoTracking()
                .SingleOrDefaultAsync(x => x.Slug == Slug);

            if (topic is null)
            {
                ErrorMessage = "Topic not found.";
                return;
            }

            TopicName = topic.Name;
            TopicDescription = string.IsNullOrWhiteSpace(topic.Description) ? "No description yet." : topic.Description;
            BoardId = topic.BoardId;
            CreatedAtUtc = topic.CreatedAtUtc;

            var board = await DbContext.Boards
                .AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == topic.BoardId);
            IsBoardLocked = board?.IsLocked ?? false;

            PostCount = await DbContext.BoardPosts
                .AsNoTracking()
                .CountAsync(x => x.BoardId == topic.BoardId && !x.IsDeleted);

            CommentCount = await DbContext.BoardPostComments
                .AsNoTracking()
                .Join(
                    DbContext.BoardPosts.AsNoTracking(),
                    comment => comment.PostId,
                    post => post.Id,
                    (comment, post) => new { comment, post })
                .CountAsync(x => x.post.BoardId == topic.BoardId && !x.comment.IsDeleted);
        }
        finally
        {
            IsLoading = false;
        }
    }
}
