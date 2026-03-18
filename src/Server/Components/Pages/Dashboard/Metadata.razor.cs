namespace Server.Components.Pages.Dashboard;

public partial class Metadata
{
    private readonly MetadataModel Model = new();
    private Task SaveAsync() => Task.CompletedTask;

    private sealed class MetadataModel
    {
        public string PackageName { get; set; } = string.Empty;
        public string Category { get; set; } = "General";
        public string Description { get; set; } = string.Empty;
        public string RepositoryUrl { get; set; } = string.Empty;
        public string WebsiteUrl { get; set; } = string.Empty;
        public string IconUrl { get; set; } = string.Empty;
        public bool IsPublic { get; set; } = true;
    }
}