using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Server.Data;

namespace Server.Services;

public interface ILinkContentGuard
{
    /// <summary>Returns null when content is allowed, otherwise a short reason.</summary>
    Task<string?> GetBlockReasonAsync(string? text, CancellationToken cancellationToken = default);
}

public sealed class LinkContentGuard(ApplicationDbContext db) : ILinkContentGuard
{
    private static readonly Regex UrlLike = new(
        """(?:https?://|www\.)[^\s"'<>()]+""",
        RegexOptions.IgnoreCase | RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(250));

    public async Task<string?> GetBlockReasonAsync(string? text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var patterns = await db.BlockedLinkPatterns
            .AsNoTracking()
            .Select(x => x.Pattern)
            .ToListAsync(cancellationToken);

        if (patterns.Count == 0)
        {
            return null;
        }

        foreach (Match match in UrlLike.Matches(text))
        {
            var segment = match.Value;
            foreach (var pattern in patterns)
            {
                if (string.IsNullOrWhiteSpace(pattern))
                {
                    continue;
                }

                if (segment.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                {
                    return "This content contains a link that is not allowed on this registry.";
                }
            }
        }

        return null;
    }
}
