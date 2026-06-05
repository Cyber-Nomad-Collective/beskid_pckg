using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Server.Components.Shared;
using Server.Data;
using Server.Services;

namespace Server.Components.Pages;

public partial class BoardPost
{
    [Parameter]
    public int PostId { get; set; }

    [Inject]
    public HttpClient ApiHttp { get; set; } = default!;

    [Inject]
    public IHtmlSanitizationService HtmlSanitization { get; set; } = default!;

    private BoardPostDetail? Post;
    private List<BoardCommentDetail> Comments = [];
    private bool IsLoading = true;
    private bool IsSubmittingReply;
    private string? ErrorMessage;
    private string ReplyContent = string.Empty;
    private ThemedRichTextEditor? ReplyEditor;

    private string SanitizedContent => Post is null ? string.Empty : Sanitize(Post.Content);
    private int NetScore => Post is null ? 0 : Post.UpvoteCount - Post.DownvoteCount;
    private string ScoreClass => NetScore switch
    {
        > 0 => "is-positive",
        < 0 => "is-negative",
        _ => string.Empty
    };

    private string BackHref => Post?.BoardEntityType switch
    {
        "Package" when !string.IsNullOrWhiteSpace(Post.BoardEntityId)
            => $"/packages/{Post.BoardEntityId}?tab=pkg-tab-community",
        _ => "/packages"
    };

    protected override async Task OnParametersSetAsync()
    {
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = null;

        try
        {
            var postResponse = await ApiHttp.GetAsync($"/api/boards/posts/{PostId}");
            if (!postResponse.IsSuccessStatusCode)
            {
                ErrorMessage = postResponse.StatusCode == System.Net.HttpStatusCode.NotFound
                    ? "This thread could not be found."
                    : "Unable to load this thread.";
                Post = null;
                Comments = [];
                return;
            }

            Post = await postResponse.Content.ReadFromJsonAsync<BoardPostDetail>();

            var commentsResponse = await ApiHttp.GetAsync($"/api/boards/posts/{PostId}/comments");
            if (commentsResponse.IsSuccessStatusCode)
            {
                var payload = await commentsResponse.Content.ReadFromJsonAsync<CommentsPayload>();
                Comments = payload?.Comments ?? [];
            }
            else
            {
                Comments = [];
            }
        }
        catch
        {
            ErrorMessage = "Unable to load this thread.";
            Post = null;
            Comments = [];
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task VotePostAsync(int voteValue)
    {
        if (Post is null)
        {
            return;
        }

        var response = await ApiHttp.PostAsJsonAsync(
            $"/api/boards/posts/{Post.Id}/vote",
            new VoteBoardPostRequest(voteValue));

        if (!response.IsSuccessStatusCode)
        {
            return;
        }

        var result = await response.Content.ReadFromJsonAsync<VoteBoardPostResponse>();
        if (result is null)
        {
            return;
        }

        var currentVote = Post.CurrentUserVote;
        var newVote = currentVote == voteValue ? 0 : voteValue;
        Post = Post with
        {
            UpvoteCount = result.UpvoteCount,
            DownvoteCount = result.DownvoteCount,
            CurrentUserVote = newVote
        };
    }

    private async Task SubmitReplyAsync()
    {
        if (Post is null || Post.IsLocked)
        {
            return;
        }

        if (ReplyEditor is not null)
        {
            ReplyContent = await ReplyEditor.GetHtmlAsync();
            await ReplyEditor.SyncToBoundValueAsync();
        }

        if (string.IsNullOrWhiteSpace(ReplyContent))
        {
            return;
        }

        IsSubmittingReply = true;
        try
        {
            var response = await ApiHttp.PostAsJsonAsync(
                $"/api/boards/posts/{Post.Id}/comments",
                new CreateBoardCommentRequest(ReplyContent.Trim(), null));

            if (response.IsSuccessStatusCode)
            {
                ReplyContent = string.Empty;
                await LoadAsync();
            }
        }
        finally
        {
            IsSubmittingReply = false;
        }
    }

    private string Sanitize(string html) => HtmlSanitization.Sanitize(html);

    private static string GetPostTypeLabel(BoardPostType type) => type switch
    {
        BoardPostType.Issue => "Issue",
        BoardPostType.FeatureRequest => "Feature",
        BoardPostType.Suggestion => "Suggestion",
        _ => "Thread"
    };

    private static string GetPostTypeColor(BoardPostType type) => type switch
    {
        BoardPostType.Issue => "#dc3545",
        BoardPostType.FeatureRequest => "#6f42c1",
        BoardPostType.Suggestion => "#198754",
        _ => "#6c757d"
    };

    private sealed record BoardPostDetail(
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
        string? BoardEntityId);

    private sealed record CommentsPayload(List<BoardCommentDetail> Comments);

    private sealed record BoardCommentDetail(
        int Id,
        int PostId,
        int? ParentCommentId,
        string AuthorUserId,
        string Content,
        DateTime CreatedAtUtc,
        DateTime? EditedAtUtc,
        int UpvoteCount,
        int DownvoteCount,
        int CurrentUserVote);

    private sealed record VoteBoardPostRequest(int VoteValue);

    private sealed record VoteBoardPostResponse(
        [property: JsonPropertyName("success")] bool Success,
        [property: JsonPropertyName("message")] string Message,
        [property: JsonPropertyName("upvoteCount")] int UpvoteCount,
        [property: JsonPropertyName("downvoteCount")] int DownvoteCount);

    private sealed record CreateBoardCommentRequest(string Content, int? ParentCommentId);
}
