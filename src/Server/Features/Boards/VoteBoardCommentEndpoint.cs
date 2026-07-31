using System.Security.Claims;
using FastEndpoints;
using Server.Data;
using Server.Services;
using Server.Services.Notifications;

namespace Server.Features.Boards;

public sealed class VoteBoardCommentEndpoint : Endpoint<VoteBoardCommentRequest, VoteBoardCommentResponse>
{
    public ApplicationDbContext Db { get; set; } = default!;
    public IUserRatingService RatingService { get; set; } = default!;
    public ICaptchaVerificationService Captcha { get; set; } = default!;
    public ILinkContentGuard LinkGuard { get; set; } = default!;
    public INotificationService Notifications { get; set; } = default!;
    public override void Configure() { Post("/boards/comments/{commentId}/vote"); Roles("User", "SuperAdmin"); }
    public override async Task HandleAsync(VoteBoardCommentRequest req, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId)) { await Send.UnauthorizedAsync(ct); return; }
        var result = await new BoardMutationService(Db, Captcha, LinkGuard, RatingService, Notifications).VoteCommentAsync(Route<int>("commentId"), userId, req.VoteValue, ct);
        if (result.Value is null) { await Send.NotFoundAsync(ct); return; }
        await Send.OkAsync(new VoteBoardCommentResponse(true, "Vote recorded.", result.Value.Upvotes, result.Value.Downvotes), ct);
    }
}
public sealed record VoteBoardCommentRequest(int VoteValue);
public sealed record VoteBoardCommentResponse(bool Success, string Message, int UpvoteCount, int DownvoteCount);
