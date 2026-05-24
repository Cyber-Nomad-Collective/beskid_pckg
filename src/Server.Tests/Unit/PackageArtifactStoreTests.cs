using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Server.Services;

namespace Server.Tests.Unit;

public class PackageArtifactStoreTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly PackageArtifactStore _store;

    public PackageArtifactStoreTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "pckg_store_tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storage:ArtifactsRootPath"] = Path.Combine(_tempRoot, "artifacts")
            })
            .Build();

        _store = new PackageArtifactStore(new TestHostEnvironment(_tempRoot), configuration);
    }

    [Fact]
    public async Task SaveAndOpenRead_RoundTrip_Works()
    {
        var payload = new byte[] { 1, 2, 3, 4, 5, 6 };
        await using var input = new MemoryStream(payload);

        var saved = await _store.SaveAsync("Demo", "1.0.0", input);
        var opened = await _store.OpenReadAsync(saved.StorageKey);

        Assert.NotNull(opened);
        await using var stream = opened!.Value.Stream;
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory);
        Assert.Equal(payload, memory.ToArray());
        Assert.True(await _store.VerifyChecksumAsync(saved.StorageKey, saved.ChecksumSha256));
        Assert.False(await _store.VerifyChecksumAsync(saved.StorageKey, "deadbeef"));
    }

    public void Dispose()
    {
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
