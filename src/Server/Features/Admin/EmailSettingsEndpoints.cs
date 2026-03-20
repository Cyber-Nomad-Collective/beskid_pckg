using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using Server.Data;

namespace Server.Features.Admin;

public sealed class GetEmailSettingsEndpoint : EndpointWithoutRequest<GetEmailSettingsResponse>
{
    public ApplicationDbContext Db { get; set; } = default!;

    public override void Configure()
    {
        Get("/admin/email-settings");
        Roles("SuperAdmin");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var settings = await Db.EmailSettings.AsNoTracking().FirstOrDefaultAsync(e => e.Id == 1, ct)
                       ?? new EmailSettingsEntity();
        await Send.OkAsync(new GetEmailSettingsResponse(
            settings.SmtpHost,
            settings.SmtpPort,
            settings.EnableSsl,
            settings.Username,
            "********",
            settings.FromEmail,
            settings.FromName), ct);
    }
}

public sealed class UpdateEmailSettingsEndpoint : Endpoint<UpdateEmailSettingsRequest>
{
    public ApplicationDbContext Db { get; set; } = default!;

    public override void Configure()
    {
        Post("/admin/email-settings");
        Roles("SuperAdmin");
    }

    public override async Task HandleAsync(UpdateEmailSettingsRequest req, CancellationToken ct)
    {
        var settings = await Db.EmailSettings.FirstOrDefaultAsync(e => e.Id == 1, ct);
        if (settings is null)
        {
            settings = new EmailSettingsEntity { Id = 1 };
            await Db.EmailSettings.AddAsync(settings, ct);
        }

        settings.SmtpHost = req.SmtpHost;
        settings.SmtpPort = req.SmtpPort;
        settings.EnableSsl = req.EnableSsl;
        settings.Username = req.Username;
        if (!string.IsNullOrWhiteSpace(req.Password) && req.Password != "********")
        {
            settings.Password = req.Password;
        }
        settings.FromEmail = req.FromEmail;
        settings.FromName = req.FromName;

        await Db.SaveChangesAsync(ct);
        await Send.OkAsync(new { ok = true }, ct);
    }
}

public sealed record GetEmailSettingsResponse(
    string? SmtpHost,
    int SmtpPort,
    bool EnableSsl,
    string? Username,
    string? Password,
    string FromEmail,
    string FromName);

public sealed class UpdateEmailSettingsRequest
{
    public string? SmtpHost { get; set; }
    public int SmtpPort { get; set; }
    public bool EnableSsl { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string FromEmail { get; set; } = "no-reply@beskid";
    public string FromName { get; set; } = "Beskid Pckg";
}
