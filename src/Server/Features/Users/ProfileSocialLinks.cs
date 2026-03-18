using System.Text.Json;
using Server.Components.Shared;
using pckg.Data;

namespace Server.Features.Users;

public sealed record ProfileSocialLink(SocialPlatform Platform, string Url);

public static class ProfileSocialLinks
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static IReadOnlyList<ProfileSocialLink> FromUser(ApplicationUser? user)
    {
        if (user is null)
        {
            return [];
        }

        var stored = Deserialize(user.SocialLinksJson);
        return stored.Count > 0
            ? stored
            : FromLegacy(user.GitHubUrl, user.WebsiteUrl, user.XUrl);
    }

    public static IReadOnlyList<ProfileSocialLink> FromLegacy(params string?[] urls)
    {
        var links = new List<ProfileSocialLink>(urls.Length);
        foreach (var url in urls)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                continue;
            }

            var trimmed = url.Trim();
            links.Add(new ProfileSocialLink(SocialPlatformCatalog.DetectPlatform(trimmed), trimmed));
        }

        return links;
    }

    public static IReadOnlyList<ProfileSocialLink> Normalize(IEnumerable<ProfileSocialLink>? links)
    {
        if (links is null)
        {
            return [];
        }

        return links
            .Where(x => !string.IsNullOrWhiteSpace(x.Url))
            .Select(x => new ProfileSocialLink(x.Platform, x.Url.Trim()))
            .Take(10)
            .ToList();
    }

    public static IReadOnlyList<ProfileSocialLink> Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            var items = JsonSerializer.Deserialize<List<ProfileSocialLink>>(json, JsonOptions);
            return Normalize(items);
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public static string Serialize(IEnumerable<ProfileSocialLink>? links)
    {
        var normalized = Normalize(links);
        return normalized.Count == 0
            ? string.Empty
            : JsonSerializer.Serialize(normalized, JsonOptions);
    }

    public static string GetLegacyUrl(IEnumerable<ProfileSocialLink>? links, SocialPlatform platform)
    {
        return Normalize(links)
            .FirstOrDefault(x => x.Platform == platform)
            ?.Url ?? string.Empty;
    }
}

