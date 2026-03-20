using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Server.Data;
using Server.Services;

namespace Server.Tests.Unit;

public class ApiPrincipalResolverTests : IAsyncDisposable
{
    private readonly string _dbPath;
    private readonly ApplicationDbContext _dbContext;
    private readonly IPasswordHasher<ApiKeyEntity> _hasher = new PasswordHasher<ApiKeyEntity>();

    public ApiPrincipalResolverTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), "pckg_resolver_tests", $"{Guid.NewGuid():N}.db");
        Directory.CreateDirectory(Path.GetDirectoryName(_dbPath)!);

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite($"Data Source={_dbPath}")
            .Options;

        _dbContext = new ApplicationDbContext(options);
        _dbContext.Database.EnsureCreated();
    }

    [Fact]
    public async Task ResolveUserIdAsync_Resolves_From_XApiKey_Header()
    {
        var plain = "bpk_test_valid_key";
        var entity = new ApiKeyEntity
        {
            Id = Guid.NewGuid(),
            UserId = "user-1",
            Name = "test",
            Prefix = plain[..10],
            ScopesCsv = "publish",
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };
        entity.KeyHash = _hasher.HashPassword(entity, plain);
        _dbContext.ApiKeys.Add(entity);
        await _dbContext.SaveChangesAsync();

        var validator = new ApiKeyValidator(_dbContext, _hasher);
        var resolver = new ApiPrincipalResolver(validator);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-API-Key"] = plain;

        var userId = await resolver.ResolveUserIdAsync(httpContext);

        Assert.Equal("user-1", userId);
    }

    [Fact]
    public async Task ResolveUserIdAsync_Returns_Null_For_Revoked_Key()
    {
        var plain = "bpk_test_revoked_key";
        var entity = new ApiKeyEntity
        {
            Id = Guid.NewGuid(),
            UserId = "user-1",
            Name = "test",
            Prefix = plain[..10],
            ScopesCsv = "publish",
            CreatedAtUtc = DateTimeOffset.UtcNow,
            RevokedAtUtc = DateTimeOffset.UtcNow,
        };
        entity.KeyHash = _hasher.HashPassword(entity, plain);
        _dbContext.ApiKeys.Add(entity);
        await _dbContext.SaveChangesAsync();

        var validator = new ApiKeyValidator(_dbContext, _hasher);
        var resolver = new ApiPrincipalResolver(validator);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["Authorization"] = $"Bearer {plain}";

        var userId = await resolver.ResolveUserIdAsync(httpContext);

        Assert.Null(userId);
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        if (File.Exists(_dbPath))
        {
            File.Delete(_dbPath);
        }
    }
}
