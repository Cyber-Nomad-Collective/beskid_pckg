using System.Text.Json;

namespace Server.Services;

/// <summary>Validates packed <c>.beskid/template.json</c> for template package publish.</summary>
public static class TemplateManifestValidator
{
    public const string ExpectedSchema = "beskid.template.v1";

    public static (bool IsValid, string Message) ValidateJson(string templateJsonText)
    {
        if (string.IsNullOrWhiteSpace(templateJsonText))
        {
            return (false, $"{PackageTemplatePaths.TemplateJsonRelativePath} is empty.");
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(templateJsonText);
        }
        catch (JsonException ex)
        {
            return (false, $"{PackageTemplatePaths.TemplateJsonRelativePath} is not valid JSON: {ex.Message}");
        }

        using (document)
        {
            var schema = document.RootElement.TryGetProperty("schema", out var schemaProp)
                ? schemaProp.GetString()
                : null;

            if (!string.Equals(schema, ExpectedSchema, StringComparison.Ordinal))
            {
                return (false, $"{PackageTemplatePaths.TemplateJsonRelativePath} schema must be '{ExpectedSchema}'.");
            }
        }

        return (true, "template.json validated.");
    }
}
