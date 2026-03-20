namespace Server.Services.Email;

public interface IEmailSender
{
    Task SendAsync(string userId, string subject, string htmlBody, CancellationToken ct = default);
}
