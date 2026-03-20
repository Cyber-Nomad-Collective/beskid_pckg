using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using Server.Data;
using Server.Services.Email;

namespace Server.Features.Admin;

public sealed class RunWeeklySpotlightEndpoint : EndpointWithoutRequest
{
    public ApplicationDbContext Db { get; set; } = default!;
    public IEmailTemplateService Templater { get; set; } = default!;
    public IEmailSender Email { get; set; } = default!;

    public override void Configure()
    {
        Post("/admin/notifications/weekly-spotlight/run");
        Roles("SuperAdmin");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var since = DateTimeOffset.UtcNow.AddDays(-7);

        // Get users who opted in to spotlight with their allowed types
        var prefs = await Db.NotificationPreferences
            .AsNoTracking()
            .Where(p => p.IncludeInSpotlight)
            .GroupBy(p => p.UserId)
            .Select(g => new { UserId = g.Key, Allowed = g.Select(p => p.Type).ToList() })
            .ToListAsync(ct);

        var sent = 0;
        foreach (var pref in prefs)
        {
            var notes = await Db.Notifications
                .AsNoTracking()
                .Where(n => n.UserId == pref.UserId && n.CreatedAtUtc >= since && pref.Allowed.Contains(n.Type))
                .OrderByDescending(n => n.CreatedAtUtc)
                .Take(100)
                .ToListAsync(ct);

            if (notes.Count == 0) continue;

            var items = string.Join("", notes.Select(n =>
                $"<li><strong>{System.Net.WebUtility.HtmlEncode(n.Title)}</strong><br/><span style='color:#9ca3af'>{n.CreatedAtUtc:u}</span>" +
                (string.IsNullOrWhiteSpace(n.Message) ? "" : $"<div>{System.Net.WebUtility.HtmlEncode(n.Message)}</div>") +
                "</li>"));

            var body = $"<p>Here is your weekly spotlight summary:</p><ul style='line-height:1.6'>{items}</ul>";
            var html = Templater.Render("Weekly spotlight", body);
            await Email.SendAsync(pref.UserId, "Weekly spotlight", html, ct);
            sent++;
        }

        await Send.OkAsync(new { ok = true, sent }, ct);
    }
}
