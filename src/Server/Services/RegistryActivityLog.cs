using Microsoft.EntityFrameworkCore;
using Server.Data;

namespace Server.Services;

public sealed record RegistryActivityEntry(
    DateTimeOffset TimestampUtc,
    string Severity,
    string Action,
    string Message,
    string? TraceId,
    string? UserId,
    string? PackageName,
    string? Version);

public interface IPckgRegistryActivityLog
{
    Task AppendAsync(RegistryActivityEntry entry, CancellationToken cancellationToken = default);

    IReadOnlyList<RegistryActivityEntry> GetRecent(int take);
}

/// <summary>
/// PostgreSQL-backed registry activity log (retains the newest 500 rows) for SuperAdmin diagnostics.
/// </summary>
public sealed class PckgRegistryActivityLog(ApplicationDbContext dbContext) : IPckgRegistryActivityLog
{
    private const int MaxEntries = 500;

    public async Task AppendAsync(RegistryActivityEntry entry, CancellationToken cancellationToken = default)
    {
        dbContext.RegistryActivities.Add(new RegistryActivityEntity
        {
            TimestampUtc = entry.TimestampUtc,
            Severity = entry.Severity,
            Action = entry.Action,
            Message = entry.Message,
            TraceId = entry.TraceId,
            UserId = entry.UserId,
            PackageName = entry.PackageName,
            Version = entry.Version,
        });

        await dbContext.SaveChangesAsync(cancellationToken);

        var excess = await dbContext.RegistryActivities
            .OrderByDescending(x => x.TimestampUtc)
            .ThenByDescending(x => x.Id)
            .Skip(MaxEntries)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        if (excess.Count == 0)
        {
            return;
        }

        await dbContext.RegistryActivities
            .Where(x => excess.Contains(x.Id))
            .ExecuteDeleteAsync(cancellationToken);
    }

    public IReadOnlyList<RegistryActivityEntry> GetRecent(int take)
    {
        if (take <= 0)
        {
            return Array.Empty<RegistryActivityEntry>();
        }

        take = Math.Min(take, MaxEntries);

        return dbContext.RegistryActivities
            .AsNoTracking()
            .OrderByDescending(x => x.TimestampUtc)
            .ThenByDescending(x => x.Id)
            .Take(take)
            .Select(x => new RegistryActivityEntry(
                x.TimestampUtc,
                x.Severity,
                x.Action,
                x.Message,
                x.TraceId,
                x.UserId,
                x.PackageName,
                x.Version))
            .ToList();
    }
}
