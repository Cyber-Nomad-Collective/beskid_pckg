using System.Net.Http.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.FluentUI.AspNetCore.Components;

namespace Server.Components.Pages.Dashboard.Admin;

public partial class AdminEmail
{
    private bool IsSaving;
    private string? FeedbackMessage;
    private MessageIntent? FeedbackIntent;
    private EmailSettingsModel EmailSettings = new();

    [Inject]
    public HttpClient ApiHttp { get; set; } = default!;

    protected override async Task OnInitializedAsync() => await LoadEmailSettingsAsync();

    private async Task LoadEmailSettingsAsync()
    {
        try
        {
            var resp = await ApiHttp.GetAsync("/api/admin/email-settings");
            if (!resp.IsSuccessStatusCode)
            {
                return;
            }

            var payload = await resp.Content.ReadFromJsonAsync<GetEmailSettingsResponse>();
            if (payload is null)
            {
                return;
            }

            EmailSettings = new EmailSettingsModel
            {
                SmtpHost = payload.SmtpHost,
                SmtpPort = payload.SmtpPort,
                EnableSsl = payload.EnableSsl,
                Username = payload.Username,
                Password = payload.Password ?? string.Empty,
                FromEmail = payload.FromEmail,
                FromName = payload.FromName,
            };
        }
        catch
        {
            // ignore load errors; form stays at defaults
        }
    }

    private async Task SaveEmailSettingsAsync()
    {
        IsSaving = true;
        FeedbackMessage = null;
        FeedbackIntent = null;
        try
        {
            await ApiHttp.PostAsJsonAsync("/api/admin/email-settings", EmailSettings);
            SetFeedback("Email settings saved.", MessageIntent.Success);
        }
        catch
        {
            SetFeedback("Could not save email settings.", MessageIntent.Error);
        }
        finally
        {
            IsSaving = false;
        }
    }

    private void SetFeedback(string message, MessageIntent intent)
    {
        FeedbackMessage = message;
        FeedbackIntent = intent;
    }

    private sealed class EmailSettingsModel
    {
        public string? SmtpHost { get; set; }
        public int SmtpPort { get; set; } = 587;
        public bool EnableSsl { get; set; } = true;
        public string? Username { get; set; }
        public string Password { get; set; } = string.Empty;
        public string FromEmail { get; set; } = "no-reply@beskid";
        public string FromName { get; set; } = "Beskid Pckg";
    }
}
