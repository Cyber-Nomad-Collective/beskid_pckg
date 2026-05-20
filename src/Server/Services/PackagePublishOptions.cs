namespace Server.Services;

public sealed class PackagePublishOptions
{
    public const string SectionName = "Pckg:Publish";

    /// <summary>
    /// When true, published .bpk artifacts must contain <see cref="PackageDocsPaths.StructuredApiDocRelativePath"/>.
    /// </summary>
    public bool RequireStructuredApiDoc { get; set; } = true;
}
