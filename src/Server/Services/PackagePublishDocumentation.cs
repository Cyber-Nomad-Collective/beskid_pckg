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

    public static void EnsureStructuredApiDoc(
        IReadOnlyDictionary<string, byte[]> memberEntries,
        string packageId)
    {
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
