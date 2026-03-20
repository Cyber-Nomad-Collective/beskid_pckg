namespace Server.Services.Email;

public sealed class EmailTemplateService : IEmailTemplateService
{
    private const string Layout = """
<!doctype html>
<html>
  <head>
    <meta charset=\"utf-8\" />
    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1\" />
    <title>{{title}}</title>
    <style>
      body{font-family: ui-sans-serif, system-ui, -apple-system, Segoe UI, Roboto, Helvetica, Arial, \"Apple Color Emoji\", \"Segoe UI Emoji\"; background:#0b0f1a; color:#e5e7eb;}
      .container{max-width:640px; margin:24px auto; background:#0f1629; border:1px solid #1f2a44; border-radius:12px; overflow:hidden}
      .header{padding:16px 20px; background:#111827; border-bottom:1px solid #1f2a44}
      .header h1{margin:0; font-size:18px}
      .content{padding:20px}
      .footer{padding:16px 20px; font-size:12px; color:#9ca3af; border-top:1px solid #1f2a44}
      a{color:#60a5fa}
    </style>
  </head>
  <body>
    <div class=\"container\">
      <div class=\"header\"><h1>{{title}}</h1></div>
      <div class=\"content\">{{body}}</div>
      <div class=\"footer\">Beskid pckg • Do not reply to this automated email.</div>
    </div>
  </body>
</html>
""";

    public string Render(string title, string bodyHtml)
    {
        return Layout
            .Replace("{{title}}", EscapeHtml(title))
            .Replace("{{body}}", bodyHtml);
    }

    private static string EscapeHtml(string value)
        => value
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;");
}
