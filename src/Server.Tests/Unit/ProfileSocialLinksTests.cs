using pckg.Data;
using Server.Components.Shared;
using Server.Features.Users;

namespace Server.Tests.Unit;

public class ProfileSocialLinksTests
{
    [Fact]
    public void FromUser_FallsBack_To_Legacy_Fields_When_Json_Is_Empty()
    {
        var user = new ApplicationUser
        {
            GitHubUrl = "https://github.com/example",
            WebsiteUrl = "https://example.com",
            XUrl = "https://x.com/example"
        };

        var links = ProfileSocialLinks.FromUser(user);

        Assert.Collection(
            links,
            link =>
            {
                Assert.Equal(SocialPlatform.GitHub, link.Platform);
                Assert.Equal("https://github.com/example", link.Url);
            },
            link =>
            {
                Assert.Equal(SocialPlatform.Website, link.Platform);
                Assert.Equal("https://example.com", link.Url);
            },
            link =>
            {
                Assert.Equal(SocialPlatform.X, link.Platform);
                Assert.Equal("https://x.com/example", link.Url);
            });
    }

    [Fact]
    public void Serialize_And_Deserialize_RoundTrip_Preserves_Order()
    {
        var links = new[]
        {
            new ProfileSocialLink(SocialPlatform.LinkedIn, "https://linkedin.com/in/example"),
            new ProfileSocialLink(SocialPlatform.GitHub, "https://github.com/example"),
            new ProfileSocialLink(SocialPlatform.Website, "https://example.com")
        };

        var json = ProfileSocialLinks.Serialize(links);
        var restored = ProfileSocialLinks.Deserialize(json);

        Assert.Collection(
            restored,
            link =>
            {
                Assert.Equal(SocialPlatform.LinkedIn, link.Platform);
                Assert.Equal("https://linkedin.com/in/example", link.Url);
            },
            link =>
            {
                Assert.Equal(SocialPlatform.GitHub, link.Platform);
                Assert.Equal("https://github.com/example", link.Url);
            },
            link =>
            {
                Assert.Equal(SocialPlatform.Website, link.Platform);
                Assert.Equal("https://example.com", link.Url);
            });
    }

    [Fact]
    public void GetLegacyUrl_Returns_First_Matching_Platform()
    {
        var links = new[]
        {
            new ProfileSocialLink(SocialPlatform.Website, "https://one.example.com"),
            new ProfileSocialLink(SocialPlatform.GitHub, "https://github.com/example"),
            new ProfileSocialLink(SocialPlatform.Website, "https://two.example.com")
        };

        var websiteUrl = ProfileSocialLinks.GetLegacyUrl(links, SocialPlatform.Website);
        var githubUrl = ProfileSocialLinks.GetLegacyUrl(links, SocialPlatform.GitHub);

        Assert.Equal("https://one.example.com", websiteUrl);
        Assert.Equal("https://github.com/example", githubUrl);
    }
}

