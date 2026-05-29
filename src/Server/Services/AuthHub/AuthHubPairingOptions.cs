namespace Server.Services.AuthHub;

public sealed class AuthHubPairingOptions
{
    public const string SectionName = "AuthHub";

    public string? HubPublicUrl { get; set; }
    public string? PublicUrl { get; set; }
    public string? PairingApproverLogin { get; set; }
    public string? GitHubSyncToken { get; set; }
}
