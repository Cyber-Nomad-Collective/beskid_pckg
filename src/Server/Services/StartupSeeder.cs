using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Server.Data;
using Server.Features.Users;

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
        await EnsureUserRatingSchemaAsync(cancellationToken);
        await EnsurePackageSchemaAsync(cancellationToken);
        await EnsurePackageTagSchemaAsync(cancellationToken);
        await EnsurePackageVersionSchemaAsync(cancellationToken);
        await EnsureTopicSchemaAsync(cancellationToken);
        await SeedExamplePackageAndTopicAsync(cancellationToken);
        await EnsureRoleAsync("SuperAdmin");
            await EnsureNotificationsSchemaAsync(cancellationToken);
            await EnsureNotificationPreferencesSchemaAsync(cancellationToken);
            await EnsureEmailSettingsSchemaAsync(cancellationToken);
            await EnsureFollowsSchemaAsync(cancellationToken);
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

        if (!existingColumns.Contains("SocialLinksJson", StringComparer.OrdinalIgnoreCase))
        {
            await dbContext.Database.ExecuteSqlRawAsync(
                "ALTER TABLE AspNetUsers ADD COLUMN SocialLinksJson TEXT NOT NULL DEFAULT '';",
                cancellationToken);
        }

        var users = await dbContext.Users.ToListAsync(cancellationToken);
        var hasChanges = false;

        foreach (var user in users)
        {
            var normalizedJson = ProfileSocialLinks.Serialize(ProfileSocialLinks.FromUser(user));
            if (string.Equals(user.SocialLinksJson, normalizedJson, StringComparison.Ordinal))
            {
                continue;
            }

            user.SocialLinksJson = normalizedJson;
            hasChanges = true;
        }

        if (hasChanges)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
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

    private async Task EnsurePackageTagSchemaAsync(CancellationToken cancellationToken)
    {
        await dbContext.Database.ExecuteSqlRawAsync(
            @"CREATE TABLE IF NOT EXISTS PackageTags (
                Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                PackageId TEXT NOT NULL,
                Tag TEXT NOT NULL,
                CreatedAtUtc TEXT NOT NULL
            );",
            cancellationToken);

        await dbContext.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS IX_PackageTags_PackageId ON PackageTags(PackageId);",
            cancellationToken);

        await dbContext.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS IX_PackageTags_Tag ON PackageTags(Tag);",
            cancellationToken);
    }

    private async Task EnsureTopicSchemaAsync(CancellationToken cancellationToken)
    {
        await dbContext.Database.ExecuteSqlRawAsync(
            @"CREATE TABLE IF NOT EXISTS Topics (
                Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                Slug TEXT NOT NULL,
                Description TEXT NULL,
                CreatedByUserId TEXT NOT NULL,
                BoardId INTEGER NOT NULL,
                CreatedAtUtc TEXT NOT NULL
            );",
            cancellationToken);

        await dbContext.Database.ExecuteSqlRawAsync(
            "CREATE UNIQUE INDEX IF NOT EXISTS IX_Topics_Slug ON Topics(Slug);",
            cancellationToken);

        await dbContext.Database.ExecuteSqlRawAsync(
            "CREATE UNIQUE INDEX IF NOT EXISTS IX_Topics_BoardId ON Topics(BoardId);",
            cancellationToken);
    }

    private async Task EnsureUserRatingSchemaAsync(CancellationToken cancellationToken)
    {
        var existingColumns = await dbContext.Database
            .SqlQueryRaw<string>("SELECT name FROM pragma_table_info('UserRatings')")
            .ToListAsync(cancellationToken);

        if (!existingColumns.Contains("KarmaPoints", StringComparer.OrdinalIgnoreCase))
        {
            await dbContext.Database.ExecuteSqlRawAsync(
                "ALTER TABLE UserRatings ADD COLUMN KarmaPoints INTEGER NOT NULL DEFAULT 0;",
                cancellationToken);
        }
    }

    private async Task SeedExamplePackageAndTopicAsync(CancellationToken cancellationToken)
    {
        var hasPackages = await dbContext.Packages.AsNoTracking().AnyAsync(cancellationToken);
        if (!hasPackages)
        {
            var packageId = Guid.NewGuid();
            var now = DateTimeOffset.UtcNow;

            dbContext.Packages.Add(new PackageEntity
            {
                Id = packageId,
                OwnerUserId = "system",
                Name = "beskid.hello-world",
                Category = "Utilities",
                Description = "Starter demo package seeded on first launch so public search is never empty.",
                RepositoryUrl = "https://github.com/cyber-nomad-collective/beskid",
                WebsiteUrl = "https://beskid.dev",
                IsPublic = true,
                TotalDownloads = 1200,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });

            dbContext.PackageTags.AddRange(
                new PackageTagEntity { PackageId = packageId, Tag = "starter", CreatedAtUtc = now },
                new PackageTagEntity { PackageId = packageId, Tag = "demo", CreatedAtUtc = now },
                new PackageTagEntity { PackageId = packageId, Tag = "official", CreatedAtUtc = now });
        }

        var hasTopics = await dbContext.Topics.AsNoTracking().AnyAsync(cancellationToken);
        if (!hasTopics)
        {
            var board = new BoardEntity
            {
                Name = "General Beskid",
                Slug = "t-general-beskid",
                Description = "General public discussion around Beskid packages and ecosystem.",
                EntityType = "Topic",
                EntityId = "general-beskid",
                CreatedAtUtc = DateTime.UtcNow,
                IsLocked = false
            };
            dbContext.Boards.Add(board);
            await dbContext.SaveChangesAsync(cancellationToken);

            dbContext.Topics.Add(new TopicEntity
            {
                Name = "General Beskid",
                Slug = "general-beskid",
                Description = "Community space for public package and ecosystem discussion.",
                CreatedByUserId = "system",
                BoardId = board.Id,
                CreatedAtUtc = DateTime.UtcNow
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

        private async Task EnsureNotificationsSchemaAsync(CancellationToken cancellationToken)
        {
            await dbContext.Database.ExecuteSqlRawAsync(
                @"CREATE TABLE IF NOT EXISTS Notifications (
                    Id TEXT NOT NULL PRIMARY KEY,
                    UserId TEXT NOT NULL,
                    Type INTEGER NOT NULL,
                    Title TEXT NOT NULL,
                    Message TEXT NULL,
                    DataJson TEXT NULL,
                    IsRead INTEGER NOT NULL DEFAULT 0,
                    CreatedAtUtc TEXT NOT NULL
                );",
                cancellationToken);

            await dbContext.Database.ExecuteSqlRawAsync(
                "CREATE INDEX IF NOT EXISTS IX_Notifications_UserId_IsRead_CreatedAtUtc ON Notifications(UserId, IsRead, CreatedAtUtc);",
                cancellationToken);
        }

        private async Task EnsureNotificationPreferencesSchemaAsync(CancellationToken cancellationToken)
        {
            await dbContext.Database.ExecuteSqlRawAsync(
                @"CREATE TABLE IF NOT EXISTS NotificationPreferences (
                    Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                    UserId TEXT NOT NULL,
                    Type INTEGER NOT NULL,
                    SendEmail INTEGER NOT NULL DEFAULT 0,
                    IncludeInSpotlight INTEGER NOT NULL DEFAULT 0
                );",
                cancellationToken);

            await dbContext.Database.ExecuteSqlRawAsync(
                "CREATE UNIQUE INDEX IF NOT EXISTS IX_NotificationPreferences_UserId_Type ON NotificationPreferences(UserId, Type);",
                cancellationToken);
        }

        private async Task EnsureEmailSettingsSchemaAsync(CancellationToken cancellationToken)
        {
            await dbContext.Database.ExecuteSqlRawAsync(
                @"CREATE TABLE IF NOT EXISTS EmailSettings (
                    Id INTEGER NOT NULL PRIMARY KEY,
                    SmtpHost TEXT NULL,
                    SmtpPort INTEGER NOT NULL DEFAULT 587,
                    EnableSsl INTEGER NOT NULL DEFAULT 1,
                    Username TEXT NULL,
                    Password TEXT NULL,
                    FromEmail TEXT NOT NULL DEFAULT 'no-reply@beskid',
                    FromName TEXT NOT NULL DEFAULT 'Beskid Pckg'
                );",
                cancellationToken);

            // Ensure a single default row exists
            await dbContext.Database.ExecuteSqlRawAsync(
                "INSERT OR IGNORE INTO EmailSettings (Id) VALUES (1);",
                cancellationToken);
        }

        private async Task EnsureFollowsSchemaAsync(CancellationToken cancellationToken)
        {
            await dbContext.Database.ExecuteSqlRawAsync(
                @"CREATE TABLE IF NOT EXISTS PackageFollows (
                    Id TEXT NOT NULL PRIMARY KEY,
                    UserId TEXT NOT NULL,
                    PackageId TEXT NOT NULL,
                    CreatedAtUtc TEXT NOT NULL
                );",
                cancellationToken);

            await dbContext.Database.ExecuteSqlRawAsync(
                @"CREATE UNIQUE INDEX IF NOT EXISTS IX_PackageFollows_User_Package ON PackageFollows(UserId, PackageId);",
                cancellationToken);

            await dbContext.Database.ExecuteSqlRawAsync(
                @"CREATE TABLE IF NOT EXISTS PublisherFollows (
                    Id TEXT NOT NULL PRIMARY KEY,
                    UserId TEXT NOT NULL,
                    PublisherUserId TEXT NOT NULL,
                    CreatedAtUtc TEXT NOT NULL
                );",
                cancellationToken);

            await dbContext.Database.ExecuteSqlRawAsync(
                @"CREATE UNIQUE INDEX IF NOT EXISTS IX_PublisherFollows_User_Publisher ON PublisherFollows(UserId, PublisherUserId);",
                cancellationToken);
        }
}
