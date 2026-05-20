using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace Server.Tests.TestUtils;

public static class WorkspaceBundleTestBuilder
{
    public static byte[] CreateTwoMemberWorkspaceBundle()
    {
        var files = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Workspace.proj"] = """
                workspace {
                  name = "DemoWorkspace"
                  resolver = v1
                }

                member "foundation" {
                  path = "foundation"
                }

                member "consumer" {
                  path = "consumer"
                }
                """,
            ["foundation/Project.proj"] = """
                project {
                  name = "Pkg.Foundation"
                  version = "0.1.0"
                }

                target "Lib" {
                  kind = Lib
                  entry = "Prelude.bd"
                }
                """,
            ["foundation/src/Prelude.bd"] = "// foundation",
            ["foundation/.beskid/docs/api.json"] = BpkTestArtifactBuilder.MinimalStructuredApiJson,
            ["consumer/.beskid/docs/api.json"] = BpkTestArtifactBuilder.MinimalStructuredApiJson,
            ["consumer/Project.proj"] = """
                project {
                  name = "Pkg.Consumer"
                  version = "0.1.0"
                }

                dependency "Pkg.Foundation" {
                  source = "path"
                  path = "../foundation"
                }

                target "Lib" {
                  kind = Lib
                  entry = "Main.bd"
                }
                """,
            ["consumer/src/Main.bd"] = "// consumer",
        };

        return CreateZip(files.ToDictionary(pair => pair.Key, pair => Encoding.UTF8.GetBytes(pair.Value)));
    }

    private static byte[] CreateZip(IReadOnlyDictionary<string, byte[]> entries)
    {
        using var memory = new MemoryStream();
        using (var zip = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var entry in entries.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                var zipEntry = zip.CreateEntry(entry.Key, CompressionLevel.Fastest);
                using var stream = zipEntry.Open();
                stream.Write(entry.Value);
            }
        }

        return memory.ToArray();
    }
}
