using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Server.Data;

namespace Server.Services;

public interface IDatabaseMigrationService
{
    Task ApplyAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Applies EF Core migrations and Postgres-specific legacy schema repairs on startup.
/// Does not seed users, API keys, or demo data.
/// </summary>
public sealed class DatabaseMigrationService(
    ApplicationDbContext dbContext,
    ILogger<DatabaseMigrationService> logger) : IDatabaseMigrationService
{
    internal readonly record struct LegacySchemaSnapshot(
        bool HasAspNetUsers,
        bool HasAspNetRoles,
        bool HasPackages,
        bool HasDataProtectionKeys,
        bool HasBlockedLinkPatterns,
        bool HasAspNetUsersPublisherVerifiedColumn);

    public async Task ApplyAsync(CancellationToken cancellationToken = default)
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
                await ApplyPostMigrationRepairsAsync(cancellationToken);
                return;
            }
            catch (PostgresException ex) when (ex.SqlState == "42P07")
            {
                logger.LogWarning(
                    ex,
                    "Legacy schema detected while applying migrations. Reconstructing migration history from existing schema objects.");
                await EnsureLegacyMigrationHistoryAsync(cancellationToken);
                await dbContext.Database.MigrateAsync(cancellationToken);
                await ApplyPostMigrationRepairsAsync(cancellationToken);
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
        await ApplyPostMigrationRepairsAsync(cancellationToken);
    }

    private async Task ApplyPostMigrationRepairsAsync(CancellationToken cancellationToken)
    {
        await EnsureAspNetUsersPublisherVerifiedColumnAsync(cancellationToken);
        await EnsurePostgresLegacyBooleanColumnsAsync(cancellationToken);
        await EnsurePostgresLegacyGuidColumnsAsync(cancellationToken);
        await EnsurePostgresLegacyIdentityColumnsAsync(cancellationToken);
    }

    private async Task EnsureAspNetUsersPublisherVerifiedColumnAsync(CancellationToken cancellationToken)
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
                    FROM information_schema.tables
                    WHERE table_schema = 'public' AND table_name = 'AspNetUsers'
                ) THEN
                    IF EXISTS (
                        SELECT 1
                        FROM information_schema.columns
                        WHERE table_schema = 'public'
                          AND table_name = 'AspNetUsers'
                          AND column_name = 'IsPublisherVerified'
                          AND data_type IN ('smallint', 'integer', 'bigint')
                    ) THEN
                        ALTER TABLE "AspNetUsers" ALTER COLUMN "IsPublisherVerified" DROP DEFAULT;
                        ALTER TABLE "AspNetUsers" ALTER COLUMN "IsPublisherVerified" TYPE boolean USING ("IsPublisherVerified" <> 0);
                    END IF;

                    IF NOT EXISTS (
                        SELECT 1
                        FROM information_schema.columns
                        WHERE table_schema = 'public'
                          AND table_name = 'AspNetUsers'
                          AND column_name = 'IsPublisherVerified'
                    ) THEN
                        ALTER TABLE "AspNetUsers" ADD COLUMN "IsPublisherVerified" boolean;
                    END IF;

                    UPDATE "AspNetUsers"
                    SET "IsPublisherVerified" = false
                    WHERE "IsPublisherVerified" IS NULL;

                    ALTER TABLE "AspNetUsers" ALTER COLUMN "IsPublisherVerified" SET DEFAULT false;
                    ALTER TABLE "AspNetUsers" ALTER COLUMN "IsPublisherVerified" SET NOT NULL;
                END IF;
            END
            $$;
            """,
            cancellationToken);
    }

    private async Task EnsurePostgresLegacyBooleanColumnsAsync(CancellationToken cancellationToken)
    {
        if (!dbContext.Database.IsNpgsql())
        {
            return;
        }

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

    private async Task EnsureLegacyMigrationHistoryAsync(CancellationToken cancellationToken)
    {
        var migrations = dbContext.Database.GetMigrations().ToArray();
        if (migrations.Length == 0)
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

        var schemaSnapshot = await ReadLegacySchemaSnapshotAsync(cancellationToken);
        var migrationIdsToRecord = DetermineLegacyMigrationsToRecord(migrations, schemaSnapshot);
        foreach (var migrationId in migrationIdsToRecord)
        {
            await dbContext.Database.ExecuteSqlAsync(
                $"""
                INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
                VALUES ({migrationId}, {"10.0.5"})
                ON CONFLICT ("MigrationId") DO NOTHING;
                """,
                cancellationToken);
        }
    }

    internal static IReadOnlyList<string> DetermineLegacyMigrationsToRecord(
        IEnumerable<string> migrations,
        LegacySchemaSnapshot snapshot)
    {
        var result = new List<string>();
        foreach (var migration in migrations)
        {
            if (migration.EndsWith("_InitialPostgres", StringComparison.Ordinal)
                && snapshot is { HasAspNetUsers: true, HasAspNetRoles: true, HasPackages: true })
            {
                result.Add(migration);
                continue;
            }

            if (migration.EndsWith("_DataProtectionKeys", StringComparison.Ordinal) && snapshot.HasDataProtectionKeys)
            {
                result.Add(migration);
                continue;
            }

            if (migration.EndsWith("_BlockedLinkPatterns", StringComparison.Ordinal) && snapshot.HasBlockedLinkPatterns)
            {
                result.Add(migration);
                continue;
            }

            if (migration.EndsWith("_ApplicationUserPublisherVerified", StringComparison.Ordinal)
                && snapshot.HasAspNetUsersPublisherVerifiedColumn)
            {
                result.Add(migration);
            }
        }

        return result;
    }

    internal static bool NeedsPublisherVerifiedRepair(LegacySchemaSnapshot snapshot)
        => snapshot.HasAspNetUsers && !snapshot.HasAspNetUsersPublisherVerifiedColumn;

    private async Task<LegacySchemaSnapshot> ReadLegacySchemaSnapshotAsync(CancellationToken cancellationToken)
    {
        await using var command = dbContext.Database.GetDbConnection().CreateCommand();
        if (command.Connection is null)
        {
            throw new InvalidOperationException("Database connection unavailable.");
        }

        var shouldCloseConnection = command.Connection.State == ConnectionState.Closed;
        if (shouldCloseConnection)
        {
            await command.Connection.OpenAsync(cancellationToken);
        }

        try
        {
            command.CommandText =
                """
                SELECT
                    EXISTS (
                        SELECT 1 FROM information_schema.tables
                        WHERE table_schema = 'public' AND table_name = 'AspNetUsers'
                    ) AS "HasAspNetUsers",
                    EXISTS (
                        SELECT 1 FROM information_schema.tables
                        WHERE table_schema = 'public' AND table_name = 'AspNetRoles'
                    ) AS "HasAspNetRoles",
                    EXISTS (
                        SELECT 1 FROM information_schema.tables
                        WHERE table_schema = 'public' AND table_name = 'Packages'
                    ) AS "HasPackages",
                    EXISTS (
                        SELECT 1 FROM information_schema.tables
                        WHERE table_schema = 'public' AND table_name = 'DataProtectionKeys'
                    ) AS "HasDataProtectionKeys",
                    EXISTS (
                        SELECT 1 FROM information_schema.tables
                        WHERE table_schema = 'public' AND table_name = 'BlockedLinkPatterns'
                    ) AS "HasBlockedLinkPatterns",
                    EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_schema = 'public' AND table_name = 'AspNetUsers' AND column_name = 'IsPublisherVerified'
                    ) AS "HasAspNetUsersPublisherVerifiedColumn";
                """;

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return default;
            }

            return new LegacySchemaSnapshot(
                reader.GetBoolean(0),
                reader.GetBoolean(1),
                reader.GetBoolean(2),
                reader.GetBoolean(3),
                reader.GetBoolean(4),
                reader.GetBoolean(5));
        }
        finally
        {
            if (shouldCloseConnection)
            {
                await command.Connection.CloseAsync();
            }
        }
    }
}
