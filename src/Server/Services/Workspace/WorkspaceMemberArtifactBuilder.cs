using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace Server.Services.Workspace;

public static class WorkspaceMemberArtifactBuilder
{
    public static byte[] BuildArtifact(
        IReadOnlyDictionary<string, byte[]> memberEntries,
        string packageJson,
        string projectProj)
    {
        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var (path, content) in memberEntries)
        {
            if (string.Equals(path, "package.json", StringComparison.OrdinalIgnoreCase)
                || string.Equals(path, "checksums.sha256", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            files[path] = content;
        }

        files["Project.proj"] = Encoding.UTF8.GetBytes(projectProj);
        files["package.json"] = Encoding.UTF8.GetBytes(packageJson);

        var checksumLines = files
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => $"{Sha256Hex(pair.Value)}  {pair.Key}")
            .ToList();
        files["checksums.sha256"] = Encoding.UTF8.GetBytes(string.Join('\n', checksumLines) + "\n");

        using var memory = new MemoryStream();
        using (var zip = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (path, content) in files.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                var entry = zip.CreateEntry(path, CompressionLevel.Fastest);
                using var stream = entry.Open();
                stream.Write(content);
            }
        }

        return memory.ToArray();
    }

    private static string Sha256Hex(byte[] bytes)
        => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
}
