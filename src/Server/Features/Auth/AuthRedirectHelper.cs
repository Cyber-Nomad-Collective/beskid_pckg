namespace Server.Features.Auth;

internal static class AuthRedirectHelper
{
    internal static string? SanitizeReturnUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (!trimmed.StartsWith("/", StringComparison.Ordinal)
            || trimmed.StartsWith("//", StringComparison.Ordinal))
        {
            return null;
        }

        return trimmed;
    }
}
