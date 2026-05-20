namespace Server.Data;

public sealed class RegistryActivityEntity
{
    public long Id { get; set; }
    public DateTimeOffset TimestampUtc { get; set; }
    public required string Severity { get; set; }
    public required string Action { get; set; }
    public required string Message { get; set; }
    public string? TraceId { get; set; }
    public string? UserId { get; set; }
    public string? PackageName { get; set; }
    public string? Version { get; set; }
}
