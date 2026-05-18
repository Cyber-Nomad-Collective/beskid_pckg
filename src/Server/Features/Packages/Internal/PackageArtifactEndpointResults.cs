using FastEndpoints;

namespace Server.Features.Packages.Internal;

/// <summary>
/// Maps artifact service status codes to FastEndpoints send helpers.
/// Uses <see cref="HttpResponseExtensions"/> because <c>Send</c> is only available on the endpoint instance.
/// </summary>
public static class PackageArtifactEndpointResults
{
    public static async Task<bool> TrySendErrorAsync(BaseEndpoint endpoint, int statusCode, CancellationToken ct)
    {
        if (statusCode == StatusCodes.Status200OK)
        {
            return false;
        }

        var response = endpoint.HttpContext.Response;
        if (statusCode == StatusCodes.Status404NotFound)
        {
            await response.SendNotFoundAsync(ct);
            return true;
        }

        await response.SendStringAsync(string.Empty, statusCode, cancellation: ct);
        return true;
    }
}
