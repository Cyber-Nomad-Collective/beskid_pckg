using FastEndpoints;

namespace pckg.Features.Users;

public sealed class BecomePublisherEndpoint
    : EndpointWithoutRequest<AuthActionResponse>
{
    public override void Configure()
    {
        Post("/users/become-publisher");
        Options(x => x.RequireAuthorization());
        Summary(s => s.Summary = "Deprecated: publishing is enabled for all authenticated users.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        await Send.OkAsync(new AuthActionResponse(true, "Publishing is enabled for all signed-in users."), ct);
    }
}
