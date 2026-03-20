using Microsoft.EntityFrameworkCore;
using Server.Data;

namespace Server.Services.Email;

public sealed class SmtpEmailSender(ApplicationDbContext db, IEmailTemplateService templater) : IEmailSender
{
    public async Task SendAsync(string userId, string subject, string htmlBody, CancellationToken ct = default)
    {
        // Resolve recipient emails for the user (primary if any, otherwise all)
        var emails = await db.UserEmails
            .AsNoTracking()
            .Where(e => e.UserId == userId)
            .OrderByDescending(e => e.IsPrimary)
            .ThenBy(e => e.IsVerified)
            .Select(e => e.Email)
            .ToListAsync(ct);

        // Fallback to the Identity email if no user-specific emails are configured
        if (emails.Count == 0)
        {
            var identityEmail = await db.Users.AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => u.Email)
                .FirstOrDefaultAsync(ct);
            if (!string.IsNullOrWhiteSpace(identityEmail))
            {
                emails.Add(identityEmail!);
            }
        }

        if (emails.Count == 0) return; // no recipient

        var settings = await db.EmailSettings.AsNoTracking().FirstOrDefaultAsync(e => e.Id == 1, ct)
                       ?? new EmailSettingsEntity();

        var body = templater.Render(subject, htmlBody);

        try
        {
            using var message = new System.Net.Mail.MailMessage
            {
                From = new System.Net.Mail.MailAddress(settings.FromEmail, settings.FromName),
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            };
            foreach (var to in emails.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                message.To.Add(to);
            }

            using var client = new System.Net.Mail.SmtpClient(settings.SmtpHost ?? "localhost", settings.SmtpPort)
            {
                EnableSsl = settings.EnableSsl
            };
            if (!string.IsNullOrWhiteSpace(settings.Username))
            {
                client.Credentials = new System.Net.NetworkCredential(settings.Username, settings.Password);
            }

            await client.SendMailAsync(message, ct);
        }
        catch
        {
            // swallow for now; consider logging later
        }
    }
}
