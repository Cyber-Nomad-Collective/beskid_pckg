using System.Collections.Concurrent;

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
    void Append(RegistryActivityEntry entry);

    IReadOnlyList<RegistryActivityEntry> GetRecent(int take);
}

/// <summary>
/// In-memory ring buffer of registry-related actions for SuperAdmin diagnostics (not a full audit log).
/// </summary>
public sealed class PckgRegistryActivityLog : IPckgRegistryActivityLog
{
    private const int MaxEntries = 500;
    private readonly ConcurrentQueue<RegistryActivityEntry> _queue = new();

    public void Append(RegistryActivityEntry entry)
    {
        _queue.Enqueue(entry);
        while (_queue.Count > MaxEntries && _queue.TryDequeue(out _))
        {
        }
    }

    public IReadOnlyList<RegistryActivityEntry> GetRecent(int take)
    {
        if (take <= 0)
        {
            return Array.Empty<RegistryActivityEntry>();
        }

        var snapshot = _queue.ToArray();
        if (snapshot.Length == 0)
        {
            return Array.Empty<RegistryActivityEntry>();
        }

        // Newest first
        var ordered = snapshot.OrderByDescending(e => e.TimestampUtc).Take(take).ToList();
        return ordered;
    }
}
