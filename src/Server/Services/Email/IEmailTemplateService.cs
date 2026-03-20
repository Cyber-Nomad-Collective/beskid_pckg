namespace Server.Services.Email;

public interface IEmailTemplateService
{
    string Render(string title, string bodyHtml);
}
