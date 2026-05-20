using Server.Services;

namespace Server.Tests.Unit;

public class PackageReadmeResolverTests
{
    [Fact]
    public void CandidatePaths_Prefers_Manifest_Then_Default_Readme()
    {
        var paths = PackageReadmeResolver.CandidatePaths("docs/overview.md");

        Assert.Equal(2, paths.Count);
        Assert.Equal("docs/overview.md", paths[0]);
        Assert.Equal("README.md", paths[1]);
    }

    [Fact]
    public void CandidatePaths_Deduplicates_When_Manifest_Points_At_Readme()
    {
        var paths = PackageReadmeResolver.CandidatePaths("README.md");

        Assert.Single(paths);
        Assert.Equal("README.md", paths[0]);
    }

    [Fact]
    public void CandidatePaths_Ignores_Unsafe_Or_NonMarkdown_Paths()
    {
        var paths = PackageReadmeResolver.CandidatePaths("../src/secret.bsk");

        Assert.Single(paths);
        Assert.Equal("README.md", paths[0]);
    }
}
