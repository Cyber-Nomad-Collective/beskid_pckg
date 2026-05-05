using Microsoft.AspNetCore.Identity;

namespace Server.Data;

public sealed class ApplicationUser : IdentityUser
{
    public string DisplayName { get; set; } = string.Empty;
    public string Bio { get; set; } = string.Empty;
    public string GitHubUrl { get; set; } = string.Empty;
    public string WebsiteUrl { get; set; } = string.Empty;
    public string XUrl { get; set; } = string.Empty;
    public string SocialLinksJson { get; set; } = string.Empty;
    public string ProfileImageUrl { get; set; } = string.Empty;

    /// <summary>When true, registry UI may show a verified badge for this account as a publisher.</summary>
    public bool IsPublisherVerified { get; set; }
}
