namespace Server.Services.Artifacts;

public static class PackageZipPathNormalizer
{
    public static string Normalize(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        return path.Replace('\\', '/').TrimStart('/');
    }
}
