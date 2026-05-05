using Server.Features.Packages;

namespace Server.Tests.Unit;

public class PackageEmbedUrlsTests
{
    [Fact]
    public void BadgeRelativePath_Encodes_Package_Query()
    {
        var path = PackageEmbedUrls.BadgeRelativePath("@pckg/demo-lib");
        Assert.Equal("/api/embed/badge.svg?package=%40pckg%2Fdemo-lib", path);
    }

    [Fact]
    public void BadgeRelativePath_Encodes_Simple_Name()
    {
        var path = PackageEmbedUrls.BadgeRelativePath("corelib");
        Assert.Equal("/api/embed/badge.svg?package=corelib", path);
    }

    [Fact]
    public void CardRelativePath_Uses_Same_Query_Key()
    {
        Assert.Equal("/api/embed/card?package=%40pckg%2Fx", PackageEmbedUrls.CardRelativePath("@pckg/x"));
    }
}
