using Server.Services;

namespace Server.Tests.Unit;

public class PackageVersioningTests
{
    [Theory]
    [InlineData(null, RegistryVersionBump.Patch)]
    [InlineData("", RegistryVersionBump.Patch)]
    [InlineData("patch", RegistryVersionBump.Patch)]
    [InlineData("PATCH", RegistryVersionBump.Patch)]
    [InlineData("minor", RegistryVersionBump.Minor)]
    [InlineData("major", RegistryVersionBump.Major)]
    public void ParseBump_Returns_Expected(string? raw, RegistryVersionBump expected)
    {
        Assert.Equal(expected, PackageVersioning.ParseBump(raw));
    }

    [Fact]
    public void ComputeNextVersion_No_Versions_Returns_0_0_1()
    {
        Assert.Equal("0.0.1", PackageVersioning.ComputeNextVersion([], RegistryVersionBump.Patch));
        Assert.Equal("0.0.1", PackageVersioning.ComputeNextVersion([], RegistryVersionBump.Minor));
    }

    [Fact]
    public void ComputeNextVersion_Bumps_From_Latest_NonYanked_Core()
    {
        Assert.Equal("1.2.4", PackageVersioning.ComputeNextVersion(["1.2.3"], RegistryVersionBump.Patch));
        Assert.Equal("1.3.0", PackageVersioning.ComputeNextVersion(["1.2.3"], RegistryVersionBump.Minor));
        Assert.Equal("2.0.0", PackageVersioning.ComputeNextVersion(["1.2.3"], RegistryVersionBump.Major));
        Assert.Equal("0.0.2", PackageVersioning.ComputeNextVersion(["0.0.1"], RegistryVersionBump.Patch));
    }

    [Fact]
    public void ComputeNextVersion_Ignores_Prerelease_For_Core()
    {
        Assert.Equal("1.0.1", PackageVersioning.ComputeNextVersion(["1.0.0-rc1"], RegistryVersionBump.Patch));
    }

    [Fact]
    public void GetLatestNonYankedVersionString_Prefers_Semver_Order()
    {
        var v = PackageVersioning.GetLatestNonYankedVersionString(
            [("1.10.0", false), ("1.2.0", false), ("1.9.0", false)]);
        Assert.Equal("1.10.0", v);
    }

    [Fact]
    public void GetLatestNonYankedVersionString_Excludes_Yanked()
    {
        var v = PackageVersioning.GetLatestNonYankedVersionString([("2.0.0", true), ("1.0.0", false)]);
        Assert.Equal("1.0.0", v);
    }
}
