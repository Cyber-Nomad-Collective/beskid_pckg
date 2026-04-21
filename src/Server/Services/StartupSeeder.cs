using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Server.Data;
using System.Security.Cryptography;
using System.Data.Common;
using Npgsql;

namespace Server.Services;

public interface IStartupSeeder
{
    Task SeedAsync(CancellationToken cancellationToken = default);
}

public sealed class StartupSeeder(
    ApplicationDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole> roleManager,
    IConfiguration configuration,
    ILogger<StartupSeeder> logger) : IStartupSeeder
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await ApplyPostgresSchemaAsync(cancellationToken);
        await EnsureRoleAsync("SuperAdmin");
        await EnsureInitialAdminUserAsync(cancellationToken);
        await SeedExamplePackageAndTopicAsync(cancellationToken);
    }

    private async Task ApplyPostgresSchemaAsync(CancellationToken cancellationToken)
    {
        if (!dbContext.Database.IsNpgsql())
        {
            return;
        }

        const int maxAttempts = 12;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                await dbContext.Database.MigrateAsync(cancellationToken);
                await EnsurePostgresLegacyBooleanColumnsAsync(cancellationToken);
                await EnsurePostgresLegacyGuidColumnsAsync(cancellationToken);
                await EnsurePostgresLegacyIdentityColumnsAsync(cancellationToken);
                return;
            }
            catch (PostgresException ex) when (ex.SqlState == "42P07")
            {
                // Existing databases may already contain tables but miss EF history.
                // Record the baseline migration and continue with compatibility fixes.
                await EnsureCurrentMigrationRecordedAsync(cancellationToken);
                await EnsurePostgresLegacyBooleanColumnsAsync(cancellationToken);
                await EnsurePostgresLegacyGuidColumnsAsync(cancellationToken);
                await EnsurePostgresLegacyIdentityColumnsAsync(cancellationToken);
                return;
            }
            catch (Exception ex) when (IsTransientStartupFailure(ex) && attempt < maxAttempts)
            {
                var delay = TimeSpan.FromSeconds(Math.Min(2 * attempt, 15));
                logger.LogWarning(
                    ex,
                    "Database startup not ready yet (attempt {Attempt}/{MaxAttempts}). Retrying in {DelaySeconds}s...",
                    attempt,
                    maxAttempts,
                    delay.TotalSeconds);
                await Task.Delay(delay, cancellationToken);
            }
        }

        await dbContext.Database.MigrateAsync(cancellationToken);
        await EnsurePostgresLegacyBooleanColumnsAsync(cancellationToken);
        await EnsurePostgresLegacyGuidColumnsAsync(cancellationToken);
        await EnsurePostgresLegacyIdentityColumnsAsync(cancellationToken);
    }

    private async Task EnsurePostgresLegacyBooleanColumnsAsync(CancellationToken cancellationToken)
    {
        if (!dbContext.Database.IsNpgsql())
        {
            return;
        }

        // Older bootstrap flows created a few logical booleans as INTEGER.
        // Normalize them before EF starts writing boolean parameters.
        var legacyBooleanColumns = new (string Table, string Column)[]
        {
            ("AspNetUsers", "EmailConfirmed"),
            ("AspNetUsers", "PhoneNumberConfirmed"),
            ("AspNetUsers", "TwoFactorEnabled"),
            ("AspNetUsers", "LockoutEnabled"),
            ("Boards", "IsLocked"),
            ("BoardPosts", "IsPinned"),
            ("BoardPosts", "IsLocked"),
            ("BoardPosts", "IsDeleted"),
            ("BoardPostComments", "IsDeleted"),
            ("Notifications", "IsRead"),
            ("UserEmails", "IsVerified"),
            ("UserEmails", "IsPrimary"),
            ("EmailSettings", "EnableSsl"),
            ("Packages", "IsPublic"),
            ("PackageVersions", "IsYanked")
        };

        foreach (var (table, column) in legacyBooleanColumns)
        {
            var sql = (table, column) switch
            {
                ("AspNetUsers", "EmailConfirmed") => """
                    DO $$
                    BEGIN
                        IF EXISTS (
                            SELECT 1 FROM information_schema.columns
                            WHERE table_schema = 'public' AND table_name = 'AspNetUsers' AND column_name = 'EmailConfirmed'
                              AND data_type IN ('smallint', 'integer', 'bigint')
                        ) THEN
                            ALTER TABLE "AspNetUsers" ALTER COLUMN "EmailConfirmed" DROP DEFAULT;
                            ALTER TABLE "AspNetUsers" ALTER COLUMN "EmailConfirmed" TYPE boolean USING ("EmailConfirmed" <> 0);
                            ALTER TABLE "AspNetUsers" ALTER COLUMN "EmailConfirmed" SET DEFAULT false;
                        END IF;
                    END
                    $$;
                    """,
                ("AspNetUsers", "PhoneNumberConfirmed") => """
                    DO $$
                    BEGIN
                        IF EXISTS (
                            SELECT 1 FROM information_schema.columns
                            WHERE table_schema = 'public' AND table_name = 'AspNetUsers' AND column_name = 'PhoneNumberConfirmed'
                              AND data_type IN ('smallint', 'integer', 'bigint')
                        ) THEN
                            ALTER TABLE "AspNetUsers" ALTER COLUMN "PhoneNumberConfirmed" DROP DEFAULT;
                            ALTER TABLE "AspNetUsers" ALTER COLUMN "PhoneNumberConfirmed" TYPE boolean USING ("PhoneNumberConfirmed" <> 0);
                            ALTER TABLE "AspNetUsers" ALTER COLUMN "PhoneNumberConfirmed" SET DEFAULT false;
                        END IF;
                    END
                    $$;
                    """,
                ("AspNetUsers", "TwoFactorEnabled") => """
                    DO $$
                    BEGIN
                        IF EXISTS (
                            SELECT 1 FROM information_schema.columns
                            WHERE table_schema = 'public' AND table_name = 'AspNetUsers' AND column_name = 'TwoFactorEnabled'
                              AND data_type IN ('smallint', 'integer', 'bigint')
                        ) THEN
                            ALTER TABLE "AspNetUsers" ALTER COLUMN "TwoFactorEnabled" DROP DEFAULT;
                            ALTER TABLE "AspNetUsers" ALTER COLUMN "TwoFactorEnabled" TYPE boolean USING ("TwoFactorEnabled" <> 0);
                            ALTER TABLE "AspNetUsers" ALTER COLUMN "TwoFactorEnabled" SET DEFAULT false;
                        END IF;
                    END
                    $$;
                    """,
                ("AspNetUsers", "LockoutEnabled") => """
                    DO $$
                    BEGIN
                        IF EXISTS (
                            SELECT 1 FROM information_schema.columns
                            WHERE table_schema = 'public' AND table_name = 'AspNetUsers' AND column_name = 'LockoutEnabled'
                              AND data_type IN ('smallint', 'integer', 'bigint')
                        ) THEN
                            ALTER TABLE "AspNetUsers" ALTER COLUMN "LockoutEnabled" DROP DEFAULT;
                            ALTER TABLE "AspNetUsers" ALTER COLUMN "LockoutEnabled" TYPE boolean USING ("LockoutEnabled" <> 0);
                            ALTER TABLE "AspNetUsers" ALTER COLUMN "LockoutEnabled" SET DEFAULT false;
                        END IF;
                    END
                    $$;
                    """,
                ("Boards", "IsLocked") => """
                    DO $$
                    BEGIN
                        IF EXISTS (
                            SELECT 1 FROM information_schema.columns
                            WHERE table_schema = 'public' AND table_name = 'Boards' AND column_name = 'IsLocked'
                              AND data_type IN ('smallint', 'integer', 'bigint')
                        ) THEN
                            ALTER TABLE "Boards" ALTER COLUMN "IsLocked" DROP DEFAULT;
                            ALTER TABLE "Boards" ALTER COLUMN "IsLocked" TYPE boolean USING ("IsLocked" <> 0);
                            ALTER TABLE "Boards" ALTER COLUMN "IsLocked" SET DEFAULT false;
                        END IF;
                    END
                    $$;
                    """,
                ("BoardPosts", "IsPinned") => """
                    DO $$
                    BEGIN
                        IF EXISTS (
                            SELECT 1 FROM information_schema.columns
                            WHERE table_schema = 'public' AND table_name = 'BoardPosts' AND column_name = 'IsPinned'
                              AND data_type IN ('smallint', 'integer', 'bigint')
                        ) THEN
                            ALTER TABLE "BoardPosts" ALTER COLUMN "IsPinned" DROP DEFAULT;
                            ALTER TABLE "BoardPosts" ALTER COLUMN "IsPinned" TYPE boolean USING ("IsPinned" <> 0);
                            ALTER TABLE "BoardPosts" ALTER COLUMN "IsPinned" SET DEFAULT false;
                        END IF;
                    END
                    $$;
                    """,
                ("BoardPosts", "IsLocked") => """
                    DO $$
                    BEGIN
                        IF EXISTS (
                            SELECT 1 FROM information_schema.columns
                            WHERE table_schema = 'public' AND table_name = 'BoardPosts' AND column_name = 'IsLocked'
                              AND data_type IN ('smallint', 'integer', 'bigint')
                        ) THEN
                            ALTER TABLE "BoardPosts" ALTER COLUMN "IsLocked" DROP DEFAULT;
                            ALTER TABLE "BoardPosts" ALTER COLUMN "IsLocked" TYPE boolean USING ("IsLocked" <> 0);
                            ALTER TABLE "BoardPosts" ALTER COLUMN "IsLocked" SET DEFAULT false;
                        END IF;
                    END
                    $$;
                    """,
                ("BoardPosts", "IsDeleted") => """
                    DO $$
                    BEGIN
                        IF EXISTS (
                            SELECT 1 FROM information_schema.columns
                            WHERE table_schema = 'public' AND table_name = 'BoardPosts' AND column_name = 'IsDeleted'
                              AND data_type IN ('smallint', 'integer', 'bigint')
                        ) THEN
                            ALTER TABLE "BoardPosts" ALTER COLUMN "IsDeleted" DROP DEFAULT;
                            ALTER TABLE "BoardPosts" ALTER COLUMN "IsDeleted" TYPE boolean USING ("IsDeleted" <> 0);
                            ALTER TABLE "BoardPosts" ALTER COLUMN "IsDeleted" SET DEFAULT false;
                        END IF;
                    END
                    $$;
                    """,
                ("BoardPostComments", "IsDeleted") => """
                    DO $$
                    BEGIN
                        IF EXISTS (
                            SELECT 1 FROM information_schema.columns
                            WHERE table_schema = 'public' AND table_name = 'BoardPostComments' AND column_name = 'IsDeleted'
                              AND data_type IN ('smallint', 'integer', 'bigint')
                        ) THEN
                            ALTER TABLE "BoardPostComments" ALTER COLUMN "IsDeleted" DROP DEFAULT;
                            ALTER TABLE "BoardPostComments" ALTER COLUMN "IsDeleted" TYPE boolean USING ("IsDeleted" <> 0);
                            ALTER TABLE "BoardPostComments" ALTER COLUMN "IsDeleted" SET DEFAULT false;
                        END IF;
                    END
                    $$;
                    """,
                ("Notifications", "IsRead") => """
                    DO $$
                    BEGIN
                        IF EXISTS (
                            SELECT 1 FROM information_schema.columns
                            WHERE table_schema = 'public' AND table_name = 'Notifications' AND column_name = 'IsRead'
                              AND data_type IN ('smallint', 'integer', 'bigint')
                        ) THEN
                            ALTER TABLE "Notifications" ALTER COLUMN "IsRead" DROP DEFAULT;
                            ALTER TABLE "Notifications" ALTER COLUMN "IsRead" TYPE boolean USING ("IsRead" <> 0);
                            ALTER TABLE "Notifications" ALTER COLUMN "IsRead" SET DEFAULT false;
                        END IF;
                    END
                    $$;
                    """,
                ("UserEmails", "IsVerified") => """
                    DO $$
                    BEGIN
                        IF EXISTS (
                            SELECT 1 FROM information_schema.columns
                            WHERE table_schema = 'public' AND table_name = 'UserEmails' AND column_name = 'IsVerified'
                              AND data_type IN ('smallint', 'integer', 'bigint')
                        ) THEN
                            ALTER TABLE "UserEmails" ALTER COLUMN "IsVerified" DROP DEFAULT;
                            ALTER TABLE "UserEmails" ALTER COLUMN "IsVerified" TYPE boolean USING ("IsVerified" <> 0);
                            ALTER TABLE "UserEmails" ALTER COLUMN "IsVerified" SET DEFAULT false;
                        END IF;
                    END
                    $$;
                    """,
                ("UserEmails", "IsPrimary") => """
                    DO $$
                    BEGIN
                        IF EXISTS (
                            SELECT 1 FROM information_schema.columns
                            WHERE table_schema = 'public' AND table_name = 'UserEmails' AND column_name = 'IsPrimary'
                              AND data_type IN ('smallint', 'integer', 'bigint')
                        ) THEN
                            ALTER TABLE "UserEmails" ALTER COLUMN "IsPrimary" DROP DEFAULT;
                            ALTER TABLE "UserEmails" ALTER COLUMN "IsPrimary" TYPE boolean USING ("IsPrimary" <> 0);
                            ALTER TABLE "UserEmails" ALTER COLUMN "IsPrimary" SET DEFAULT false;
                        END IF;
                    END
                    $$;
                    """,
                ("EmailSettings", "EnableSsl") => """
                    DO $$
                    BEGIN
                        IF EXISTS (
                            SELECT 1 FROM information_schema.columns
                            WHERE table_schema = 'public' AND table_name = 'EmailSettings' AND column_name = 'EnableSsl'
                              AND data_type IN ('smallint', 'integer', 'bigint')
                        ) THEN
                            ALTER TABLE "EmailSettings" ALTER COLUMN "EnableSsl" DROP DEFAULT;
                            ALTER TABLE "EmailSettings" ALTER COLUMN "EnableSsl" TYPE boolean USING ("EnableSsl" <> 0);
                            ALTER TABLE "EmailSettings" ALTER COLUMN "EnableSsl" SET DEFAULT false;
                        END IF;
                    END
                    $$;
                    """,
                ("Packages", "IsPublic") => """
                    DO $$
                    BEGIN
                        IF EXISTS (
                            SELECT 1 FROM information_schema.columns
                            WHERE table_schema = 'public' AND table_name = 'Packages' AND column_name = 'IsPublic'
                              AND data_type IN ('smallint', 'integer', 'bigint')
                        ) THEN
                            ALTER TABLE "Packages" ALTER COLUMN "IsPublic" DROP DEFAULT;
                            ALTER TABLE "Packages" ALTER COLUMN "IsPublic" TYPE boolean USING ("IsPublic" <> 0);
                            ALTER TABLE "Packages" ALTER COLUMN "IsPublic" SET DEFAULT false;
                        END IF;
                    END
                    $$;
                    """,
                ("PackageVersions", "IsYanked") => """
                    DO $$
                    BEGIN
                        IF EXISTS (
                            SELECT 1 FROM information_schema.columns
                            WHERE table_schema = 'public' AND table_name = 'PackageVersions' AND column_name = 'IsYanked'
                              AND data_type IN ('smallint', 'integer', 'bigint')
                        ) THEN
                            ALTER TABLE "PackageVersions" ALTER COLUMN "IsYanked" DROP DEFAULT;
                            ALTER TABLE "PackageVersions" ALTER COLUMN "IsYanked" TYPE boolean USING ("IsYanked" <> 0);
                            ALTER TABLE "PackageVersions" ALTER COLUMN "IsYanked" SET DEFAULT false;
                        END IF;
                    END
                    $$;
                    """,
                _ => null
            };

            if (!string.IsNullOrWhiteSpace(sql))
            {
                await dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);
            }
        }
    }

    private async Task EnsurePostgresLegacyGuidColumnsAsync(CancellationToken cancellationToken)
    {
        if (!dbContext.Database.IsNpgsql())
        {
            return;
        }

        // Earlier SQLite-first bootstraps persisted Guid columns as text.
        // Normalize to native uuid for Npgsql readers.
        var legacyGuidColumns = new (string Table, string Column)[]
        {
            ("ApiKeys", "Id"),
            ("Packages", "Id"),
            ("PackageCommunityReviews", "Id"),
            ("PackageCommunityReviews", "PackageId"),
            ("PackageIssues", "Id"),
            ("PackageIssues", "PackageId"),
            ("PackageIssueVotes", "Id"),
            ("PackageIssueVotes", "IssueId"),
            ("PackageReviews", "Id"),
            ("PackageReviews", "PackageId"),
            ("PackageVersions", "Id"),
            ("PackageVersions", "PackageId"),
            ("PackageFollows", "Id"),
            ("PublisherFollows", "Id"),
            ("Notifications", "Id"),
            ("PackageTags", "PackageId")
        };

        foreach (var (table, column) in legacyGuidColumns)
        {
            var sql = $"""
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_schema = 'public' AND table_name = '{table}' AND column_name = '{column}'
                          AND data_type IN ('text', 'character varying')
                    ) THEN
                        ALTER TABLE "{table}" ALTER COLUMN "{column}" DROP DEFAULT;
                        ALTER TABLE "{table}" ALTER COLUMN "{column}" TYPE uuid USING ("{column}"::uuid);
                    END IF;
                END
                $$;
                """;

            await dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);
        }
    }

    private static bool IsTransientStartupFailure(Exception ex)
    {
        if (ex is TimeoutException or DbException)
        {
            return true;
        }

        if (ex.GetType().FullName is "Npgsql.NpgsqlException" or "Npgsql.PostgresException")
        {
            return true;
        }

        var message = ex.ToString();
        return message.Contains("connection refused", StringComparison.OrdinalIgnoreCase)
               || message.Contains("database system is starting up", StringComparison.OrdinalIgnoreCase)
               || message.Contains("failed to connect", StringComparison.OrdinalIgnoreCase)
               || message.Contains("transient failure", StringComparison.OrdinalIgnoreCase);
    }

    private async Task EnsurePostgresLegacyIdentityColumnsAsync(CancellationToken cancellationToken)
    {
        if (!dbContext.Database.IsNpgsql())
        {
            return;
        }

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            DO $$
            BEGIN
                IF EXISTS (
                    SELECT 1
                    FROM information_schema.columns
                    WHERE table_schema = 'public'
                      AND table_name = 'Boards'
                      AND column_name = 'Id'
                      AND data_type IN ('smallint', 'integer', 'bigint')
                      AND COALESCE(is_identity, 'NO') = 'NO'
                ) THEN
                    IF to_regclass('public."Boards_Id_seq"') IS NULL THEN
                        CREATE SEQUENCE "Boards_Id_seq";
                    END IF;

                    ALTER TABLE "Boards" ALTER COLUMN "Id" SET DEFAULT nextval('"Boards_Id_seq"');
                    PERFORM setval(
                        '"Boards_Id_seq"',
                        COALESCE((SELECT MAX("Id") FROM "Boards"), 0) + 1,
                        false
                    );
                END IF;
            END
            $$;
            """,
            cancellationToken);
    }

    private async Task EnsureCurrentMigrationRecordedAsync(CancellationToken cancellationToken)
    {
        var currentMigrationId = dbContext.Database.GetMigrations().LastOrDefault();
        if (string.IsNullOrWhiteSpace(currentMigrationId))
        {
            return;
        }

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
                "MigrationId" character varying(150) NOT NULL,
                "ProductVersion" character varying(32) NOT NULL,
                CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
            );
            """,
            cancellationToken);

        await dbContext.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
            VALUES ({currentMigrationId}, {"10.0.5"})
            ON CONFLICT ("MigrationId") DO NOTHING;
            """,
            cancellationToken);
    }

    private async Task EnsureRoleAsync(string roleName)
    {
        if (!await roleManager.RoleExistsAsync(roleName))
        {
            await roleManager.CreateAsync(new IdentityRole(roleName));
        }
    }

    private async Task EnsureInitialAdminUserAsync(CancellationToken cancellationToken)
    {
        var hasUsers = await dbContext.Users.AsNoTracking().AnyAsync(cancellationToken);
        if (hasUsers)
        {
            logger.LogInformation("Skipping bootstrap admin seed because users already exist.");
            return;
        }

        var adminLogin = configuration["Security:BootstrapAdminLogin"];
        if (string.IsNullOrWhiteSpace(adminLogin))
        {
            adminLogin = configuration["Security:BootstrapAdminEmail"];
        }
        if (string.IsNullOrWhiteSpace(adminLogin))
        {
            adminLogin = "admin@example.com";
        }

        var adminEmail = configuration["Security:BootstrapAdminEmail"];
        if (string.IsNullOrWhiteSpace(adminEmail))
        {
            adminEmail = adminLogin.Contains('@', StringComparison.Ordinal)
                ? adminLogin
                : $"{adminLogin}@local.pckg";
        }

        var adminPassword = configuration["Security:BootstrapAdminPassword"];
        if (string.IsNullOrWhiteSpace(adminPassword))
        {
            adminPassword = GenerateBootstrapPassword();
            logger.LogWarning(
                "Security:BootstrapAdminPassword not set. Generated bootstrap admin password for user {AdminLogin}: {GeneratedPassword}",
                adminLogin,
                adminPassword);
        }

        var adminUser = new ApplicationUser
        {
            UserName = adminLogin,
            Email = adminEmail,
            EmailConfirmed = true,
            DisplayName = "Administrator"
        };

        var createResult = await userManager.CreateAsync(adminUser, adminPassword);
        if (!createResult.Succeeded)
        {
            var errors = string.Join("; ", createResult.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Failed to create bootstrap admin user: {errors}");
        }

        var roleResult = await userManager.AddToRoleAsync(adminUser, "SuperAdmin");
        if (!roleResult.Succeeded)
        {
            var errors = string.Join("; ", roleResult.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Failed to assign SuperAdmin role to bootstrap user: {errors}");
        }
    }

    private static string GenerateBootstrapPassword()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@#$%^&*_-+=?";
        Span<byte> bytes = stackalloc byte[20];
        RandomNumberGenerator.Fill(bytes);

        var result = new char[20];
        // Ensure compatibility with common Identity defaults.
        result[0] = 'A';
        result[1] = 'a';
        result[2] = '7';
        result[3] = '!';
        for (var i = 4; i < result.Length; i++)
        {
            result[i] = chars[bytes[i] % chars.Length];
        }

        return new string(result);
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

}
