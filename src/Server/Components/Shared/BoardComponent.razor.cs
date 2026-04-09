using Microsoft.AspNetCore.Components;
using System.Net.Http.Json;
using Server.Data;
using Server.Services;

namespace Server.Components.Shared;

public partial class BoardComponent
{
    [Parameter]
    public int BoardId { get; set; }

    [Parameter]
    public string BoardName { get; set; } = string.Empty;

    [Parameter]
    public string BoardDescription { get; set; } = string.Empty;

    [Parameter]
    public bool IsLocked { get; set; }

    [Parameter]
    public BoardPostType? FilterPostType { get; set; }

    [Parameter]
    public bool ShowPostTypeSelector { get; set; } = true;

    [Parameter]
    public BoardPostType DefaultPostType { get; set; } = BoardPostType.Issue;

    [Parameter]
    public string NewPostButtonText { get; set; } = "New Post";

    [Parameter]
    public string CreateDialogTitle { get; set; } = "Create New Post";

    [Parameter]
    public string EmptyStateText { get; set; } = "No posts yet. Be the first to start a discussion!";

    [Parameter]
    public string ViewDiscussionButtonText { get; set; } = "View Discussion";

    [Inject]
    public HttpClient ApiHttp { get; set; } = default!;

    [Inject]
    public NavigationManager Navigation { get; set; } = default!;

    [Inject]
    public IHtmlSanitizationService HtmlSanitization { get; set; } = default!;

    private List<BoardPostDto> Posts = [];
    private bool IsLoading = true;
    private int CurrentPage = 1;
    private int PageSize = 20;
    private int TotalCount;
    private int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    private bool IsComposerOpen;
    private bool IsSubmittingPost;
    private string NewPostTitle = string.Empty;
    private string NewPostContent = string.Empty;
    private string NewTagInput = string.Empty;
    private readonly List<string> NewPostTags = [];
    private ThemedRichTextEditor? ComposerEditor;
    private BoardPostType NewPostType = BoardPostType.Issue;
    private int? _loadedBoardId;
    private BoardPostType? _loadedFilterPostType;

    private string GetPostTypeLabel(BoardPostType type) => type switch
    {
        BoardPostType.Issue => "Issue",
        BoardPostType.FeatureRequest => "Feature Request",
        BoardPostType.Suggestion => "Suggestion",
        _ => "Post"
    };

    private string GetPostTypeColor(BoardPostType type) => type switch
    {
        BoardPostType.Issue => "#dc3545",
        BoardPostType.FeatureRequest => "#6f42c1",
        BoardPostType.Suggestion => "#198754",
        _ => "#6c757d"
    };

    private string RenderContentPreview(string content)
        => TruncateContent(HtmlSanitization.ToPlainText(content), 200);

    private static string TruncateContent(string content, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(content) || content.Length <= maxLength)
            return content;

        var truncated = content[..maxLength];
        var lastSpace = truncated.LastIndexOf(' ');
        if (lastSpace > maxLength / 2)
            truncated = truncated[..lastSpace];

        return truncated + "...";
    }

    protected override async Task OnInitializedAsync()
    {
        await LoadPostsAsync();
    }

    protected override async Task OnParametersSetAsync()
    {
        if (_loadedBoardId == BoardId && _loadedFilterPostType == FilterPostType)
        {
            return;
        }

        CurrentPage = 1;
        await LoadPostsAsync();
    }

    private async Task LoadPostsAsync()
    {
        IsLoading = true;
        try
        {
            _loadedBoardId = BoardId;
            _loadedFilterPostType = FilterPostType;

            var postTypeQuery = FilterPostType is null ? string.Empty : $"&postType={FilterPostType}";
            var response = await ApiHttp.GetAsync($"/api/boards/{BoardId}/posts?page={CurrentPage}&pageSize={PageSize}{postTypeQuery}");
            if (!response.IsSuccessStatusCode)
            {
                return;
            }

            var result = await response.Content.ReadFromJsonAsync<GetBoardPostsResponse>();
            if (result is not null)
            {
                Posts = result.Posts;
                TotalCount = result.TotalCount;
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task PreviousPageAsync()
    {
        if (CurrentPage > 1)
        {
            CurrentPage--;
            await LoadPostsAsync();
        }
    }

    private async Task GoToPageAsync(int page)
    {
        if (page >= 1 && page <= TotalPages && page != CurrentPage)
        {
            CurrentPage = page;
            await LoadPostsAsync();
        }
    }

    private async Task NextPageAsync()
    {
        if (CurrentPage < TotalPages)
        {
            CurrentPage++;
            await LoadPostsAsync();
        }
    }

    private void OpenComposer()
    {
        NewPostType = FilterPostType ?? DefaultPostType;
        IsComposerOpen = true;
    }

    private void CancelComposer()
    {
        IsComposerOpen = false;
        IsSubmittingPost = false;
        NewPostTitle = string.Empty;
        NewPostContent = string.Empty;
        NewTagInput = string.Empty;
        NewPostTags.Clear();
        NewPostType = FilterPostType ?? DefaultPostType;
    }

    private void AddTag()
    {
        var normalized = NewTagInput.Trim().Trim('#').ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalized) || NewPostTags.Contains(normalized, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        NewPostTags.Add(normalized);
        NewTagInput = string.Empty;
    }

    private void RemoveTag(string tag)
    {
        NewPostTags.Remove(tag);
    }

    private string BuildPostContent()
    {
        if (NewPostTags.Count == 0)
        {
            return NewPostContent.Trim();
        }

        var tagsLine = string.Join(' ', NewPostTags.Select(tag => $"#{tag}"));
        return $"{tagsLine}{Environment.NewLine}{Environment.NewLine}{NewPostContent.Trim()}";
    }

    private async Task CreatePostAsync()
    {
        if (ComposerEditor is not null)
        {
            NewPostContent = await ComposerEditor.GetHtmlAsync();
            await ComposerEditor.SyncToBoundValueAsync();
        }

        if (string.IsNullOrWhiteSpace(NewPostTitle) || string.IsNullOrWhiteSpace(NewPostContent))
        {
            return;
        }

        IsSubmittingPost = true;
        try
        {
            var response = await ApiHttp.PostAsJsonAsync(
                $"/api/boards/{BoardId}/posts",
                new CreateBoardPostRequest(NewPostTitle.Trim(), BuildPostContent(), FilterPostType ?? NewPostType));

            if (response.IsSuccessStatusCode)
            {
                CancelComposer();
                CurrentPage = 1;
                await LoadPostsAsync();
            }
        }
        finally
        {
            IsSubmittingPost = false;
        }
    }

    private async Task VotePost(int postId, int voteValue)
    {
        var response = await ApiHttp.PostAsJsonAsync(
            $"/api/boards/posts/{postId}/vote",
            new VoteBoardPostRequest(voteValue));

        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<VoteBoardPostResponse>();
            if (result is not null)
            {
                // Update the post in the list with new vote counts
                var postIndex = Posts.FindIndex(p => p.Id == postId);
                if (postIndex >= 0)
                {
                    var post = Posts[postIndex];
                    var currentVote = post.CurrentUserVote;
                    var newVote = currentVote == voteValue ? 0 : voteValue;
                    Posts[postIndex] = post with
                    {
                        UpvoteCount = result.UpvoteCount,
                        DownvoteCount = result.DownvoteCount,
                        CurrentUserVote = newVote
                    };
                    StateHasChanged();
                }
            }
        }
    }

    private void ViewPost(int postId)
    {
        Navigation.NavigateTo($"/board/post/{postId}");
    }

    private sealed record GetBoardPostsResponse(List<BoardPostDto> Posts, int TotalCount, int Page, int PageSize);
    private sealed record BoardPostDto(int Id, int BoardId, string AuthorUserId, string Title, string Content, BoardPostType PostType, DateTime CreatedAtUtc, DateTime? EditedAtUtc, int UpvoteCount, int DownvoteCount, bool IsPinned, bool IsLocked, int CurrentUserVote);
    private sealed record CreateBoardPostRequest(string Title, string Content, BoardPostType PostType);
    private sealed record VoteBoardPostRequest(int VoteValue);
    private sealed record VoteBoardPostResponse(bool Success, string Message, int UpvoteCount, int DownvoteCount);
}
