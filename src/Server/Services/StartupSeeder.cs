using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using pckg.Data;

namespace Server.Services;

public interface IStartupSeeder
{
    Task SeedAsync(CancellationToken cancellationToken = default);
}

public sealed class StartupSeeder(
    ApplicationDbContext dbContext,
    RoleManager<IdentityRole> roleManager) : IStartupSeeder
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        dbContext.Database.EnsureCreated();
        await EnsureUserProfileSchemaAsync(cancellationToken);
        await EnsurePackageSchemaAsync(cancellationToken);
        await EnsurePackageVersionSchemaAsync(cancellationToken);
        await EnsureRoleAsync("SuperAdmin");
    }

    private async Task EnsureRoleAsync(string roleName)
    {
        if (!await roleManager.RoleExistsAsync(roleName))
        {
            await roleManager.CreateAsync(new IdentityRole(roleName));
        }
    }

    private async Task EnsureUserProfileSchemaAsync(CancellationToken cancellationToken)
    {
        var existingColumns = await dbContext.Database
            .SqlQueryRaw<string>("SELECT name FROM pragma_table_info('AspNetUsers')")
            .ToListAsync(cancellationToken);

        if (!existingColumns.Contains("DisplayName", StringComparer.OrdinalIgnoreCase))
        {
            await dbContext.Database.ExecuteSqlRawAsync(
                "ALTER TABLE AspNetUsers ADD COLUMN DisplayName TEXT NOT NULL DEFAULT '';",
                cancellationToken);
        }

        if (!existingColumns.Contains("Bio", StringComparer.OrdinalIgnoreCase))
        {
            await dbContext.Database.ExecuteSqlRawAsync(
                "ALTER TABLE AspNetUsers ADD COLUMN Bio TEXT NOT NULL DEFAULT '';",
                cancellationToken);
        }

        if (!existingColumns.Contains("GitHubUrl", StringComparer.OrdinalIgnoreCase))
        {
            await dbContext.Database.ExecuteSqlRawAsync(
                "ALTER TABLE AspNetUsers ADD COLUMN GitHubUrl TEXT NOT NULL DEFAULT '';",
                cancellationToken);
        }

        if (!existingColumns.Contains("WebsiteUrl", StringComparer.OrdinalIgnoreCase))
        {
            await dbContext.Database.ExecuteSqlRawAsync(
                "ALTER TABLE AspNetUsers ADD COLUMN WebsiteUrl TEXT NOT NULL DEFAULT '';",
                cancellationToken);
        }

        if (!existingColumns.Contains("XUrl", StringComparer.OrdinalIgnoreCase))
        {
            await dbContext.Database.ExecuteSqlRawAsync(
                "ALTER TABLE AspNetUsers ADD COLUMN XUrl TEXT NOT NULL DEFAULT '';",
                cancellationToken);
        }

        if (!existingColumns.Contains("ProfileImageUrl", StringComparer.OrdinalIgnoreCase))
        {
            await dbContext.Database.ExecuteSqlRawAsync(
                "ALTER TABLE AspNetUsers ADD COLUMN ProfileImageUrl TEXT NOT NULL DEFAULT '';",
                cancellationToken);
        }
    }

    private async Task EnsurePackageSchemaAsync(CancellationToken cancellationToken)
    {
        var existingColumns = await dbContext.Database
            .SqlQueryRaw<string>("SELECT name FROM pragma_table_info('Packages')")
            .ToListAsync(cancellationToken);

        if (!existingColumns.Contains("Category", StringComparer.OrdinalIgnoreCase))
        {
            await dbContext.Database.ExecuteSqlRawAsync(
                "ALTER TABLE Packages ADD COLUMN Category TEXT NOT NULL DEFAULT 'General';",
                cancellationToken);
        }

        if (!existingColumns.Contains("IconUrl", StringComparer.OrdinalIgnoreCase))
        {
            await dbContext.Database.ExecuteSqlRawAsync(
                "ALTER TABLE Packages ADD COLUMN IconUrl TEXT NULL;",
                cancellationToken);
        }

        if (!existingColumns.Contains("TotalDownloads", StringComparer.OrdinalIgnoreCase))
        {
            await dbContext.Database.ExecuteSqlRawAsync(
                "ALTER TABLE Packages ADD COLUMN TotalDownloads INTEGER NOT NULL DEFAULT 0;",
                cancellationToken);
        }
    }

    private async Task EnsurePackageVersionSchemaAsync(CancellationToken cancellationToken)
    {
        await dbContext.Database.ExecuteSqlRawAsync(
            @"CREATE TABLE IF NOT EXISTS PackageVersions (
                Id TEXT NOT NULL PRIMARY KEY,
                PackageId TEXT NOT NULL,
                Version TEXT NOT NULL,
                ManifestJson TEXT NOT NULL,
                ChecksumSha256 TEXT NOT NULL,
                StorageKey TEXT NOT NULL,
                ContentType TEXT NOT NULL,
                SizeBytes INTEGER NOT NULL,
                IsYanked INTEGER NOT NULL DEFAULT 0,
                PublishedAtUtc TEXT NOT NULL,
                YankedAtUtc TEXT NULL,
                FOREIGN KEY(PackageId) REFERENCES Packages(Id) ON DELETE CASCADE
            );",
            cancellationToken);

        await dbContext.Database.ExecuteSqlRawAsync(
            "CREATE UNIQUE INDEX IF NOT EXISTS IX_PackageVersions_PackageId_Version ON PackageVersions(PackageId, Version);",
            cancellationToken);

        await dbContext.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS IX_PackageVersions_PublishedAtUtc ON PackageVersions(PublishedAtUtc);",
            cancellationToken);
    }
}
