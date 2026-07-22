namespace Server.Services;

/// <summary>
/// Resolves the on-disk root for user-uploaded files (profile images, etc.).
/// Defaults under ContentRoot/data so Coolify named volumes (/app/data) stay writable
/// after the image drops privileges to uid 10001.
/// </summary>
public static class UploadPaths
{
    public static string ResolveUploadsRoot(IWebHostEnvironment environment, IConfiguration configuration)
    {
        var configured = configuration["Storage:UploadsRootPath"];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured.Trim();
        }

        return Path.Combine(environment.ContentRootPath, "data", "uploads");
    }
}
