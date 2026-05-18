using Microsoft.EntityFrameworkCore;
using Server.Data;

namespace Server.Features.Packages;

public readonly record struct PublisherOwnerRow(string DisplayLabel, bool IsPublisherVerified);

internal static class PackageOwnerQueries
{
    public static string PublisherDisplayLabel(string displayName, string? userName) =>
        !string.IsNullOrWhiteSpace(displayName)
            ? displayName.Trim()
            : (userName ?? string.Empty).Trim();

    public static async Task<IReadOnlyDictionary<string, PublisherOwnerRow>> GetPublisherRowsAsync(
        this ApplicationDbContext db,
        IEnumerable<string> userIds,
        CancellationToken ct)
    {
        var ids = userIds.Where(static id => !string.IsNullOrWhiteSpace(id)).Distinct().ToArray();
        if (ids.Length == 0)
        {
            return new Dictionary<string, PublisherOwnerRow>();
        }

        var rows = await db.Users.AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .Select(u => new { u.Id, u.DisplayName, u.UserName, u.IsPublisherVerified })
            .ToListAsync(ct);

        return rows.ToDictionary(
            x => x.Id,
            x => new PublisherOwnerRow(
                PublisherDisplayLabel(x.DisplayName, x.UserName),
                x.IsPublisherVerified));
    }
}
