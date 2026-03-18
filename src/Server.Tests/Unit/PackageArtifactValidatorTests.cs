using Server.Services;
using Server.Tests.TestUtils;

namespace Server.Tests.Unit;

public class PackageArtifactValidatorTests
{
    private readonly PackageArtifactValidator _validator = new();

    [Fact]
    public async Task ValidateAsync_Accepts_Compliant_Artifact()
    {
        var bytes = BpkTestArtifactBuilder.CreateValidArtifact("Demo", "1.2.3");
        await using var stream = new MemoryStream(bytes);

        var result = await _validator.ValidateAsync(stream, "Demo", "1.2.3");

        Assert.True(result.IsValid);
        Assert.Equal("Artifact validated.", result.Message);
        Assert.Equal(BpkTestArtifactBuilder.ArtifactSha256(bytes), result.ArtifactChecksumSha256);
        Assert.Contains("\"schema\":\"beskid.package.v1\"", result.ManifestJson);
    }

    [Fact]
    public async Task ValidateAsync_Rejects_Checksum_Mismatch()
    {
        var bytes = BpkTestArtifactBuilder.CreateArtifactWithBadChecksum("Demo", "1.2.3");
        await using var stream = new MemoryStream(bytes);

        var result = await _validator.ValidateAsync(stream, "Demo", "1.2.3");

        Assert.False(result.IsValid);
        Assert.Contains("checksums.sha256", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidateAsync_Rejects_Version_Mismatch()
    {
        var bytes = BpkTestArtifactBuilder.CreateValidArtifact("Demo", "2.0.0");
        await using var stream = new MemoryStream(bytes);

        var result = await _validator.ValidateAsync(stream, "Demo", "1.0.0");

        Assert.False(result.IsValid);
        Assert.Contains("package.json version", result.Message);
    }
}
