using System.Security.Claims;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using Server.Data;
using Server.Services;
using Server.Services.Notifications;

namespace Server.Features.Boards;

public sealed class CreateBoardCommentEndpoint : Endpoint<CreateBoardCommentRequest, CreateBoardCommentResponse>
{
    public ApplicationDbContext Db { get; set; } = default!;
    public IUserRatingService RatingService { get; set; } = default!;
    public INotificationService Notifications { get; set; } = default!;
    public ICaptchaVerificationService Captcha { get; set; } = default!;
    public ILinkContentGuard LinkGuard { get; set; } = default!;

    public override void Configure()
    {
        Post("/boards/posts/{postId}/comments");
        Roles("User", "SuperAdmin", "Moderator");
    }

    public override async Task HandleAsync(CreateBoardCommentRequest req, CancellationToken ct)
    {
        var postId = Route<int>("postId");
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        var result = await new BoardMutationService(Db, Captcha, LinkGuard, RatingService, Notifications).CreateCommentAsync(postId, userId, req.Content, req.ParentCommentId, req.CaptchaToken, HttpContext.Connection.RemoteIpAddress?.ToString(), ct);
        if (result.Value is not null) { await Send.OkAsync(new CreateBoardCommentResponse(true, "Comment created successfully.", result.Value.Id), ct); return; }
        if (result.StatusCode == StatusCodes.Status404NotFound) { await Send.NotFoundAsync(ct); return; }
        await Send.ResponseAsync(new CreateBoardCommentResponse(false, result.Message!), result.StatusCode, ct);
    }
}

public sealed record CreateBoardCommentRequest(
    string Content,
    int? ParentCommentId,
    string? CaptchaToken = null);
public sealed record CreateBoardCommentResponse(bool Success, string Message, int? CommentId = null);
