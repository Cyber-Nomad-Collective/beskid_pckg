using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Server.Data;
using Server.Services;

namespace Server.Tests.Integration;

public sealed class TestApplicationFactory : WebApplicationFactory<Program>, IAsyncDisposable
{
    private readonly string _tempRoot;
    private readonly string _artifactRoot;
    private readonly string _dataProtectionKeysPath;
    private readonly string _inMemoryDatabaseName;

    public TestApplicationFactory()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "pckg_server_tests", Guid.NewGuid().ToString("N"));
        _artifactRoot = Path.Combine(_tempRoot, "artifacts");
        _dataProtectionKeysPath = Path.Combine(_tempRoot, "data-protection-keys");
        _inMemoryDatabaseName = $"pckg_integration_tests_{Guid.NewGuid():N}";
        Directory.CreateDirectory(_tempRoot);
        Directory.CreateDirectory(_artifactRoot);
        Directory.CreateDirectory(_dataProtectionKeysPath);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        // Must apply before Program.cs reads configuration (ConfigureAppConfiguration runs too late for top-level statements).
        builder.UseSetting("Security:PersistDataProtectionKeysToDatabase", "false");
        builder.UseSetting("Security:DataProtectionKeysPath", _dataProtectionKeysPath);

        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Port=5432;Database=pckgdb-tests;Username=postgres;Password=postgres",
                ["Security:UseHttpsRedirection"] = "false",
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<ApplicationDbContext>();
            services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseInMemoryDatabase(_inMemoryDatabaseName));

            services.RemoveAll<IPackageArtifactStore>();
            services.AddSingleton<IPackageArtifactStore>(sp =>
            {
                var configuration = sp.GetRequiredService<IConfiguration>();
                return new PackageArtifactStore(new TestHostEnvironment(_artifactRoot), configuration);
            });
        });
    }

    public async Task<(ApplicationUser User, string ApiKeyPlain, PackageEntity Package)> SeedOwnerWithPackageAsync(
        string packageName,
        bool isPublic = true)
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<ApiKeyEntity>>();

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString("N"),
            UserName = $"user-{Guid.NewGuid():N}@test.local",
            NormalizedUserName = $"USER-{Guid.NewGuid():N}@TEST.LOCAL",
            Email = $"user-{Guid.NewGuid():N}@test.local",
            NormalizedEmail = $"USER-{Guid.NewGuid():N}@TEST.LOCAL",
            EmailConfirmed = true,
        };

        var uniquePackageName = $"{packageName}.{Guid.NewGuid():N}";

        var package = new PackageEntity
        {
            Id = Guid.NewGuid(),
            OwnerUserId = user.Id,
            Name = uniquePackageName,
            Description = "Test package",
            IsPublic = isPublic,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        };

        var plain = $"bpk_{Guid.NewGuid():N}";
        var apiKey = new ApiKeyEntity
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Name = "default",
            Prefix = plain[..10],
            ScopesCsv = "publish",
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };
        apiKey.KeyHash = hasher.HashPassword(apiKey, plain);

        db.Users.Add(user);
        db.Packages.Add(package);
        db.ApiKeys.Add(apiKey);
        await db.SaveChangesAsync();

        return (user, plain, package);
    }

    public async Task<PackageVersionEntity?> GetPackageVersionAsync(string packageName, string version)
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var package = await db.Packages.AsNoTracking().SingleOrDefaultAsync(p => p.Name == packageName);
        if (package is null)
        {
            return null;
        }

        return await db.PackageVersions
            .AsNoTracking()
            .SingleOrDefaultAsync(v => v.PackageId == package.Id && v.Version == version);
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await Task.CompletedTask;
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    private sealed class TestHostEnvironment(string root) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "Server.Tests";
        public string ContentRootPath { get; set; } = root;
        public IFileProvider ContentRootFileProvider { get; set; } = new PhysicalFileProvider(root);
    }
}
