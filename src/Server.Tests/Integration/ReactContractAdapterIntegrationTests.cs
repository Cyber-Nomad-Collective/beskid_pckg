using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Server.Data;

namespace Server.Tests.Integration;

public sealed class ReactContractAdapterIntegrationTests : IClassFixture<TestApplicationFactory>
{
    private readonly TestApplicationFactory _factory;

    public ReactContractAdapterIntegrationTests(TestApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Publisher_contract_lists_profiles_and_owned_packages()
    {
        var (user, _, package) = await _factory.SeedOwnerWithPackageAsync("React.Publisher");
        var client = _factory.CreateClient();

        var publishers = await client.GetAsync("/api/publishers");
        var publisherPackages = await client.GetAsync($"/api/publishers/{Uri.EscapeDataString(user.Id)}/packages");

        Assert.Equal(HttpStatusCode.OK, publishers.StatusCode);
        Assert.Equal(HttpStatusCode.OK, publisherPackages.StatusCode);
        Assert.Contains(user.Id, await publishers.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Contains(package.Name, await publisherPackages.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Auth_session_contract_returns_the_authenticated_subject()
    {
        var (user, _, _) = await _factory.SeedOwnerWithPackageAsync("React.Session");
        var client = await _factory.CreateAuthenticatedPublisherClientAsync(user);

        var response = await client.GetAsync("/api/auth/session");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<SessionContract>();
        Assert.NotNull(payload);
        Assert.Equal(user.Id, payload.Subject);
        Assert.False(string.IsNullOrWhiteSpace(payload.GithubLogin));
        Assert.False(string.IsNullOrWhiteSpace(payload.HubSessionId));
    }

    [Fact]
    public async Task Community_contract_lists_boards()
    {
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Boards.Add(new BoardEntity
            {
                Name = "React community",
                Slug = $"react-{Guid.NewGuid():N}",
                Description = "React contract board",
                EntityType = "community",
                CreatedAtUtc = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        var response = await _factory.CreateClient().GetAsync("/api/community/boards");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("React community", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Community_contract_creates_posts_comments_and_votes()
    {
        var boardId = await SeedCommunityBoardAsync();
        var client = await _factory.CreateAuthenticatedSuperAdminClientAsync();

        var postResponse = await client.PostAsJsonAsync($"/api/community/boards/{boardId}/posts", new
        {
            title = "React post",
            content = "A post created through the React contract.",
        });

        Assert.Equal(HttpStatusCode.OK, postResponse.StatusCode);
        var post = await postResponse.Content.ReadFromJsonAsync<CommunityPostContract>();
        Assert.NotNull(post);
        Assert.Equal(boardId, post.BoardId);
        Assert.Equal("React post", post.Title);

        var commentResponse = await client.PostAsJsonAsync($"/api/community/boards/posts/{post.Id}/comments", new
        {
            content = "A React comment.",
        });

        Assert.Equal(HttpStatusCode.OK, commentResponse.StatusCode);
        var comment = await commentResponse.Content.ReadFromJsonAsync<CommunityCommentContract>();
        Assert.NotNull(comment);
        Assert.Equal(post.Id, comment.PostId);

        var postVote = await client.PostAsJsonAsync($"/api/community/boards/posts/{post.Id}/vote", new { value = 1 });
        var commentVote = await client.PostAsJsonAsync($"/api/community/boards/comments/{comment.Id}/vote", new { value = 1 });

        Assert.Equal(HttpStatusCode.OK, postVote.StatusCode);
        Assert.Equal(HttpStatusCode.OK, commentVote.StatusCode);
        Assert.Equal(1, (await postVote.Content.ReadFromJsonAsync<CommunityVoteContract>())!.Score);
        Assert.Equal(1, (await commentVote.Content.ReadFromJsonAsync<CommunityVoteContract>())!.Score);
    }

    [Fact]
    public async Task Community_contract_locks_the_existing_board()
    {
        var boardId = await SeedCommunityBoardAsync();
        var client = await _factory.CreateAuthenticatedSuperAdminClientAsync();

        var lockResponse = await client.PostAsJsonAsync($"/api/community/boards/{boardId}/moderation/lock", new { locked = true });

        Assert.Equal(HttpStatusCode.OK, lockResponse.StatusCode);
        var boardResponse = await client.GetAsync($"/api/community/boards/{boardId}");
        Assert.Equal(HttpStatusCode.OK, boardResponse.StatusCode);
        Assert.Contains("\"locked\":true", await boardResponse.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    private async Task<int> SeedCommunityBoardAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var board = new BoardEntity
        {
            Name = "React community",
            Slug = $"react-{Guid.NewGuid():N}",
            Description = "React contract board",
            EntityType = "community",
            CreatedAtUtc = DateTime.UtcNow,
        };
        db.Boards.Add(board);
        await db.SaveChangesAsync();
        return board.Id;
    }

    private sealed record SessionContract(string Subject, string GithubLogin, string HubSessionId);
    private sealed record CommunityPostContract(int Id, int BoardId, string Title);
    private sealed record CommunityCommentContract(int Id, int PostId);
    private sealed record CommunityVoteContract(int Score);
}
