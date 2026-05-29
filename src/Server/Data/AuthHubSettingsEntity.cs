namespace Server.Data;

public sealed class AuthHubSettingsEntity
{
    public int Id { get; set; } = 1;
    public string? HubUrl { get; set; }
    public string? ProtectedServiceToken { get; set; }
    public DateTimeOffset? PairedAtUtc { get; set; }
}
