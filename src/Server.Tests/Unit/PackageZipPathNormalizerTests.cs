using Server.Services;
using Server.Services.Artifacts;

namespace Server.Tests.Unit;

public sealed class PackageZipPathNormalizerTests
{
    [Theory]
    [InlineData("docs\\readme.md", "docs/readme.md")]
    [InlineData("/.beskid/docs/api.json", ".beskid/docs/api.json")]
    [InlineData("src/main.bd", "src/main.bd")]
    public void Normalize_converts_backslashes_and_trims_leading_slash(string input, string expected)
    {
        Assert.Equal(expected, PackageZipPathNormalizer.Normalize(input));
    }

    [Theory]
    [InlineData("docs/readme.md")]
    [InlineData(".beskid/docs/api.json")]
    public void HasOnlySafePathSegments_allows_package_doc_paths(string path)
    {
        Assert.True(PackageDocsPaths.HasOnlySafePathSegments(path));
    }

    [Theory]
    [InlineData("../secret.txt")]
    [InlineData("docs/../../etc/passwd")]
    public void HasOnlySafePathSegments_rejects_unsafe_paths(string path)
    {
        Assert.False(PackageDocsPaths.HasOnlySafePathSegments(path));
    }
}
