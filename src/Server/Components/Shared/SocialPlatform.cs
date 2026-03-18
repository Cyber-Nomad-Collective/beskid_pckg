using Blazor.Tabler.Icons;

namespace Server.Components.Shared;

public enum SocialPlatform
{
    Website,
    GitHub,
    GitLab,
    Bitbucket,
    X,
    LinkedIn,
    Facebook,
    Instagram,
    YouTube,
    Reddit,
    Discord,
    Medium,
    DevTo,
    StackOverflow,
    TikTok,
    Twitch,
    Other
}

public static class SocialPlatformCatalog
{
    public static IReadOnlyList<SocialPlatform> Platforms { get; } =
    [
        SocialPlatform.Website,
        SocialPlatform.GitHub,
        SocialPlatform.GitLab,
        SocialPlatform.Bitbucket,
        SocialPlatform.X,
        SocialPlatform.LinkedIn,
        SocialPlatform.Facebook,
        SocialPlatform.Instagram,
        SocialPlatform.YouTube,
        SocialPlatform.Reddit,
        SocialPlatform.Discord,
        SocialPlatform.Medium,
        SocialPlatform.DevTo,
        SocialPlatform.StackOverflow,
        SocialPlatform.TikTok,
        SocialPlatform.Twitch,
        SocialPlatform.Other
    ];

    public static string GetDisplayName(SocialPlatform platform) => platform switch
    {
        SocialPlatform.Website => "Website",
        SocialPlatform.GitHub => "GitHub",
        SocialPlatform.GitLab => "GitLab",
        SocialPlatform.Bitbucket => "Bitbucket",
        SocialPlatform.X => "X / Twitter",
        SocialPlatform.LinkedIn => "LinkedIn",
        SocialPlatform.Facebook => "Facebook",
        SocialPlatform.Instagram => "Instagram",
        SocialPlatform.YouTube => "YouTube",
        SocialPlatform.Reddit => "Reddit",
        SocialPlatform.Discord => "Discord",
        SocialPlatform.Medium => "Medium",
        SocialPlatform.DevTo => "Dev.to",
        SocialPlatform.StackOverflow => "Stack Overflow",
        SocialPlatform.TikTok => "TikTok",
        SocialPlatform.Twitch => "Twitch",
        _ => "Other"
    };

    public static string GetPlaceholder(SocialPlatform platform) => platform switch
    {
        SocialPlatform.GitHub => "https://github.com/username",
        SocialPlatform.GitLab => "https://gitlab.com/username",
        SocialPlatform.Bitbucket => "https://bitbucket.org/username",
        SocialPlatform.X => "https://x.com/username",
        SocialPlatform.LinkedIn => "https://linkedin.com/in/username",
        SocialPlatform.Facebook => "https://facebook.com/username",
        SocialPlatform.Instagram => "https://instagram.com/username",
        SocialPlatform.YouTube => "https://youtube.com/@channel",
        SocialPlatform.Reddit => "https://reddit.com/u/username",
        SocialPlatform.Discord => "https://discord.gg/invite",
        SocialPlatform.Medium => "https://medium.com/@username",
        SocialPlatform.DevTo => "https://dev.to/username",
        SocialPlatform.StackOverflow => "https://stackoverflow.com/users/id/name",
        SocialPlatform.TikTok => "https://tiktok.com/@username",
        SocialPlatform.Twitch => "https://twitch.tv/username",
        SocialPlatform.Other => "https://example.com/profile",
        _ => "https://your-site.dev"
    };

    public static TablerIconType GetIcon(SocialPlatform platform) => platform switch
    {
        SocialPlatform.Website => TablerIconType.World,
        SocialPlatform.GitHub => TablerIconType.BrandGithub,
        SocialPlatform.GitLab => TablerIconType.BrandGitlab,
        SocialPlatform.Bitbucket => TablerIconType.BrandBitbucket,
        SocialPlatform.X => TablerIconType.BrandX,
        SocialPlatform.LinkedIn => TablerIconType.BrandLinkedin,
        SocialPlatform.Facebook => TablerIconType.BrandFacebook,
        SocialPlatform.Instagram => TablerIconType.BrandInstagram,
        SocialPlatform.YouTube => TablerIconType.BrandYoutube,
        SocialPlatform.Reddit => TablerIconType.BrandReddit,
        SocialPlatform.Discord => TablerIconType.BrandDiscord,
        SocialPlatform.Medium => TablerIconType.BrandMedium,
        SocialPlatform.DevTo => TablerIconType.BrandDeviantart,
        SocialPlatform.StackOverflow => TablerIconType.BrandStackoverflow,
        SocialPlatform.TikTok => TablerIconType.BrandTiktok,
        SocialPlatform.Twitch => TablerIconType.BrandTwitch,
        _ => TablerIconType.Globe
    };

    public static SocialPlatform DetectPlatform(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return SocialPlatform.Website;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || string.IsNullOrWhiteSpace(uri.Host))
        {
            return SocialPlatform.Other;
        }

        var host = uri.Host.ToLowerInvariant();

        if (host.Contains("github.com", StringComparison.Ordinal)) return SocialPlatform.GitHub;
        if (host.Contains("gitlab.com", StringComparison.Ordinal)) return SocialPlatform.GitLab;
        if (host.Contains("bitbucket.org", StringComparison.Ordinal)) return SocialPlatform.Bitbucket;
        if (host.Contains("x.com", StringComparison.Ordinal) || host.Contains("twitter.com", StringComparison.Ordinal)) return SocialPlatform.X;
        if (host.Contains("linkedin.com", StringComparison.Ordinal)) return SocialPlatform.LinkedIn;
        if (host.Contains("facebook.com", StringComparison.Ordinal) || host.Contains("fb.com", StringComparison.Ordinal)) return SocialPlatform.Facebook;
        if (host.Contains("instagram.com", StringComparison.Ordinal)) return SocialPlatform.Instagram;
        if (host.Contains("youtube.com", StringComparison.Ordinal) || host.Contains("youtu.be", StringComparison.Ordinal)) return SocialPlatform.YouTube;
        if (host.Contains("reddit.com", StringComparison.Ordinal)) return SocialPlatform.Reddit;
        if (host.Contains("discord.com", StringComparison.Ordinal) || host.Contains("discord.gg", StringComparison.Ordinal)) return SocialPlatform.Discord;
        if (host.Contains("medium.com", StringComparison.Ordinal)) return SocialPlatform.Medium;
        if (host.Contains("dev.to", StringComparison.Ordinal)) return SocialPlatform.DevTo;
        if (host.Contains("stackoverflow.com", StringComparison.Ordinal)) return SocialPlatform.StackOverflow;
        if (host.Contains("tiktok.com", StringComparison.Ordinal)) return SocialPlatform.TikTok;
        if (host.Contains("twitch.tv", StringComparison.Ordinal)) return SocialPlatform.Twitch;

        return SocialPlatform.Website;
    }
}
