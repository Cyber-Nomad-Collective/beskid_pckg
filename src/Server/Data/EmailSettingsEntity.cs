namespace Server.Data;

public sealed class EmailSettingsEntity
{
    public int Id { get; set; } = 1; // single row config
    public string? SmtpHost { get; set; }
    public int SmtpPort { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string FromEmail { get; set; } = "no-reply@beskid";
    public string FromName { get; set; } = "Beskid Pckg";
}
