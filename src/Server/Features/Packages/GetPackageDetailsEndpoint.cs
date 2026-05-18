using FastEndpoints;

namespace Server.Features.Packages;

public sealed class GetPackageDetailsEndpoint(IPackageDetailsQuery packageDetails)
    : EndpointWithoutRequest<PackageDetailsResponse>
{
    public override void Configure()
    {
        Get("/packages/{IdOrName}");
        AllowAnonymous();
        Summary(s => s.Summary = "Get package details by id or name.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var idOrName = Route<string>("IdOrName")?.Trim();
        if (string.IsNullOrWhiteSpace(idOrName))
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var response = await packageDetails.GetByIdOrNameAsync(HttpContext, idOrName, ct);
        if (response is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(response, ct);
    }
}
