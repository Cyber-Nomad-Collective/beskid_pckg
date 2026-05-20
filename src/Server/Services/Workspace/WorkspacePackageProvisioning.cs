using Microsoft.EntityFrameworkCore;
using Server.Data;

namespace Server.Services.Workspace;

internal static class WorkspacePackageProvisioning
{
    /// <summary>
    /// Ensures every workspace member registry id exists as a package owned by <paramref name="userId"/>.
    /// Creates missing packages from <paramref name="workspacePackageManifest"/> metadata (or defaults).
    /// </summary>
    public static async Task<IReadOnlyList<PackageEntity>> EnsureOwnedPackagesAsync(
        ApplicationDbContext dbContext,
        string userId,
        IReadOnlyList<WorkspaceMemberPublishContext> memberContexts,
        WorkspacePublishManifest workspacePackageManifest,
        CancellationToken cancellationToken)
    {
        var membersByPackageId = memberContexts
            .GroupBy(m => m.PackageId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var packageNames = membersByPackageId.Keys.ToList();
        var existing = await dbContext.Packages
            .Where(p => packageNames.Contains(p.Name))
            .ToListAsync(cancellationToken);

        var notOwned = existing
            .Where(p => !string.Equals(p.OwnerUserId, userId, StringComparison.Ordinal))
            .Select(p => p.Name)
            .ToList();
        if (notOwned.Count > 0)
        {
            throw new InvalidOperationException(
                $"You do not own workspace packages: {string.Join(", ", notOwned)}.");
        }

        var byName = existing.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);
        var now = DateTimeOffset.UtcNow;

        foreach (var (packageId, member) in membersByPackageId)
        {
            if (byName.ContainsKey(packageId))
            {
                continue;
            }

            workspacePackageManifest.Members.TryGetValue(member.MemberId, out var memberConfig);
            var entity = new PackageEntity
            {
                Id = Guid.NewGuid(),
                OwnerUserId = userId,
                Name = packageId,
                Description = memberConfig?.Description
                    ?? $"Beskid workspace package {packageId}.",
                Category = memberConfig?.Category ?? "Library",
                RepositoryUrl = memberConfig?.RepositoryUrl,
                WebsiteUrl = memberConfig?.WebsiteUrl ?? "https://beskid-lang.org",
                IconUrl = PackageRegistryUrlNormalizer.NormalizeIconUrl(memberConfig?.IconUrl),
                IsPublic = memberConfig?.IsPublic ?? true,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
            };
            dbContext.Packages.Add(entity);
            byName[packageId] = entity;

            if (memberConfig?.Tags is { Count: > 0 } tags)
            {
                foreach (var tag in tags
                             .Where(t => !string.IsNullOrWhiteSpace(t))
                             .Select(t => t.Trim().ToLowerInvariant())
                             .Distinct(StringComparer.OrdinalIgnoreCase)
                             .Take(16))
                {
                    dbContext.PackageTags.Add(new PackageTagEntity
                    {
                        PackageId = entity.Id,
                        Tag = tag,
                        CreatedAtUtc = now,
                    });
                }
            }
        }

        if (byName.Count > existing.Count)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return packageNames.Select(name => byName[name]).ToList();
    }
}
