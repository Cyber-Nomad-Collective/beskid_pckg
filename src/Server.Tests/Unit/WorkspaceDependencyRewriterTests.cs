using Server.Services.Workspace;

namespace Server.Tests.Unit;

public class WorkspaceDependencyRewriterTests
{
    [Fact]
    public void RewriteProjectProj_Replaces_Workspace_Path_Dependency_With_Registry_Version()
    {
        var foundation = new WorkspaceMemberPublishContext(
            "foundation",
            "foundation",
            "Pkg.Foundation",
            "Pkg.Foundation",
            "1.0.0",
            new ProjectManifestLite("Pkg.Foundation", []),
            PackagePckgSection.Empty,
            new Dictionary<string, string>());

        var consumer = new WorkspaceMemberPublishContext(
            "consumer",
            "consumer",
            "Pkg.Consumer",
            "Pkg.Consumer",
            "1.0.0",
            new ProjectManifestLite(
                "Pkg.Consumer",
                [new ProjectDependencyDefinition("Pkg.Foundation", "path", "../foundation", null, null)]),
            PackagePckgSection.Empty,
            new Dictionary<string, string>());

        var index = new WorkspaceMemberIndex([foundation, consumer]);
        var published = new Dictionary<string, WorkspaceMemberPublishContext>
        {
            [foundation.PackageId] = foundation,
        };

        var rewritten = WorkspaceDependencyRewriter.RewriteProjectProj(
            """
            project {
              name = "Pkg.Consumer"
            }

            dependency "Pkg.Foundation" {
              source = "path"
              path = "../foundation"
            }
            """,
            consumer,
            index,
            published,
            new Dictionary<string, string>());

        Assert.Contains("source = \"registry\"", rewritten, StringComparison.Ordinal);
        Assert.Contains("version = \"1.0.0\"", rewritten, StringComparison.Ordinal);
        Assert.DoesNotContain("path = \"../foundation\"", rewritten, StringComparison.Ordinal);
    }

    [Fact]
    public void OrderMembersForPublish_Places_Dependencies_Before_Dependents()
    {
        var foundation = new WorkspaceMemberPublishContext(
            "foundation",
            "foundation",
            "Pkg.Foundation",
            "Pkg.Foundation",
            "1.0.0",
            new ProjectManifestLite("Pkg.Foundation", []),
            PackagePckgSection.Empty,
            new Dictionary<string, string>());

        var consumer = new WorkspaceMemberPublishContext(
            "consumer",
            "consumer",
            "Pkg.Consumer",
            "Pkg.Consumer",
            "1.0.0",
            new ProjectManifestLite(
                "Pkg.Consumer",
                [new ProjectDependencyDefinition("Pkg.Foundation", "path", "../foundation", null, null)]),
            PackagePckgSection.Empty,
            new Dictionary<string, string>());

        var ordered = WorkspaceDependencyRewriter.OrderMembersForPublish(
            [consumer, foundation],
            new WorkspaceMemberIndex([foundation, consumer]));

        Assert.Equal("foundation", ordered[0].MemberId);
        Assert.Equal("consumer", ordered[1].MemberId);
    }
}
