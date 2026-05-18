namespace Server.Services.Artifacts;

public abstract class PackageArtifactResult(int statusCode)
{
    public int StatusCode { get; } = statusCode;
}
