using Microsoft.EntityFrameworkCore;
using Server.Data;
using Server.Services;

namespace Server.Tests.Unit;

public sealed class DatabaseMigrationServiceTests
{
    [Fact]
    public void EfMetadata_ContainsPublisherVerifiedMigration()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=pckgdb-tests;Username=postgres;Password=postgres")
            .Options;

        using var dbContext = new ApplicationDbContext(options);

        var migrations = dbContext.Database.GetMigrations().ToArray();

        Assert.Contains("20260501120000_ApplicationUserPublisherVerified", migrations);
    }

    [Fact]
    public void DetermineLegacyMigrationsToRecord_DoesNotMarkPublisherVerified_WhenColumnMissing()
    {
        var migrations = new[]
        {
            "20260324120251_InitialPostgres",
            "20260325035622_DataProtectionKeys",
            "20260422211118_BlockedLinkPatterns",
            "20260501120000_ApplicationUserPublisherVerified",
        };
        var snapshot = new DatabaseMigrationService.LegacySchemaSnapshot(
            HasAspNetUsers: true,
            HasAspNetRoles: true,
            HasPackages: true,
            HasDataProtectionKeys: true,
            HasBlockedLinkPatterns: true,
            HasAspNetUsersPublisherVerifiedColumn: false);

        var recorded = DatabaseMigrationService.DetermineLegacyMigrationsToRecord(migrations, snapshot);

        Assert.Equal(
            new[]
            {
                "20260324120251_InitialPostgres",
                "20260325035622_DataProtectionKeys",
                "20260422211118_BlockedLinkPatterns",
            },
            recorded);
    }

    [Fact]
    public void DetermineLegacyMigrationsToRecord_MarksPublisherVerified_WhenColumnExists()
    {
        var migrations = new[]
        {
            "20260324120251_InitialPostgres",
            "20260325035622_DataProtectionKeys",
            "20260422211118_BlockedLinkPatterns",
            "20260501120000_ApplicationUserPublisherVerified",
        };
        var snapshot = new DatabaseMigrationService.LegacySchemaSnapshot(
            HasAspNetUsers: true,
            HasAspNetRoles: true,
            HasPackages: true,
            HasDataProtectionKeys: true,
            HasBlockedLinkPatterns: true,
            HasAspNetUsersPublisherVerifiedColumn: true);

        var recorded = DatabaseMigrationService.DetermineLegacyMigrationsToRecord(migrations, snapshot);

        Assert.Equal(migrations, recorded);
    }

    [Fact]
    public void NeedsPublisherVerifiedRepair_ReturnsTrue_WhenAspNetUsersExistsButColumnMissing()
    {
        var snapshot = new DatabaseMigrationService.LegacySchemaSnapshot(
            HasAspNetUsers: true,
            HasAspNetRoles: true,
            HasPackages: true,
            HasDataProtectionKeys: true,
            HasBlockedLinkPatterns: true,
            HasAspNetUsersPublisherVerifiedColumn: false);

        var result = DatabaseMigrationService.NeedsPublisherVerifiedRepair(snapshot);

        Assert.True(result);
    }

    [Fact]
    public void NeedsPublisherVerifiedRepair_ReturnsFalse_WhenColumnAlreadyExists()
    {
        var snapshot = new DatabaseMigrationService.LegacySchemaSnapshot(
            HasAspNetUsers: true,
            HasAspNetRoles: true,
            HasPackages: true,
            HasDataProtectionKeys: true,
            HasBlockedLinkPatterns: true,
            HasAspNetUsersPublisherVerifiedColumn: true);

        var result = DatabaseMigrationService.NeedsPublisherVerifiedRepair(snapshot);

        Assert.False(result);
    }
}
