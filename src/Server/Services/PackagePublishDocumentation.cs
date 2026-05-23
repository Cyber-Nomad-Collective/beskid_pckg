namespace Server.Services;

/// <summary>
/// Ensures published package artifacts include Beskid CLI documentation output.
/// </summary>
public static class PackagePublishDocumentation
{
    public const int DefaultApiJsonSchemaVersion = 4;

    public static bool HasStructuredApiDoc(IReadOnlyDictionary<string, byte[]> memberEntries)
        => memberEntries.Keys.Any(key =>
            string.Equals(key, PackageDocsPaths.StructuredApiDocRelativePath, StringComparison.OrdinalIgnoreCase));

    public static bool RequiresStructuredApiDoc(
        IReadOnlyDictionary<string, byte[]> memberEntries,
        string? packageJsonText = null)
    {
        if (memberEntries.Keys.Any(key =>
                string.Equals(key, PackageTemplatePaths.TemplateJsonRelativePath, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(packageJsonText))
        {
            var kind = PackageManifestMetadataReader.Read(packageJsonText).PackageKind;
            if (PackageKinds.IsTemplate(kind) || PackageKinds.IsTool(kind))
            {
                return false;
            }
        }

        return true;
    }

    public static void EnsureStructuredApiDoc(
        IReadOnlyDictionary<string, byte[]> memberEntries,
        string packageId,
        string? packageJsonText = null)
    {
        if (!RequiresStructuredApiDoc(memberEntries, packageJsonText))
        {
            return;
        }

        if (HasStructuredApiDoc(memberEntries))
        {
            return;
        }

        throw new InvalidOperationException(
            $"Package '{packageId}' is missing '{PackageDocsPaths.StructuredApiDocRelativePath}'. "
            + "Generate API docs before publish (`beskid doc --project Project.proj --out .beskid/docs` "
            + "or `beskid pckg pack`, which runs doc generation automatically) and include `.beskid/docs/` "
            + "in the workspace bundle.");
    }
}
