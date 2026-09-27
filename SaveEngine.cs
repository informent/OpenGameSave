using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
namespace OpenGameSave;
public sealed record GameProfile(string Name, string? GameFolder, string SaveFolder);
public sealed record SaveFile(string RelativePath, long Bytes, string Sha256);
public sealed record SaveSnapshot(string Id, DateTime CreatedAt, string ProfileName, string SaveFolder, IReadOnlyList<SaveFile> Files);
public static class SaveEngine
{
    public static SaveSnapshot CreateSnapshot(GameProfile profile, string library)
    {
        if (!Directory.Exists(profile.SaveFolder)) throw new DirectoryNotFoundException(profile.SaveFolder); var id = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"); var root = Path.Combine(library, profile.Name, id); Directory.CreateDirectory(root); var files = new List<SaveFile>();
        foreach (var file in Directory.EnumerateFiles(profile.SaveFolder, "*", SearchOption.AllDirectories)) { var relative = Path.GetRelativePath(profile.SaveFolder, file); var target = Path.Combine(root, relative); Directory.CreateDirectory(Path.GetDirectoryName(target)!); File.Copy(file, target); files.Add(new SaveFile(relative, new FileInfo(file).Length, Hash(file))); }
        var snapshot = new SaveSnapshot(id, DateTime.Now, profile.Name, profile.SaveFolder, files); File.WriteAllText(Path.Combine(root, "manifest.json"), JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true })); return snapshot;
    }
    public static bool Verify(string snapshotRoot) { var snapshot = Load(snapshotRoot); return snapshot.Files.All(x => { var path = Path.Combine(snapshotRoot, x.RelativePath); return File.Exists(path) && new FileInfo(path).Length == x.Bytes && Hash(path).Equals(x.Sha256, StringComparison.OrdinalIgnoreCase); }); }
    public static SaveSnapshot Load(string root) => JsonSerializer.Deserialize<SaveSnapshot>(File.ReadAllText(Path.Combine(root, "manifest.json"))) ?? throw new InvalidDataException("Invalid save snapshot manifest.");
    public static int Restore(string snapshotRoot, string target) { if (!Verify(snapshotRoot)) throw new InvalidDataException("Snapshot integrity verification failed."); var snapshot = Load(snapshotRoot); Directory.CreateDirectory(target); foreach (var file in snapshot.Files) { var destination = Path.Combine(target, file.RelativePath); if (File.Exists(destination)) throw new IOException($"Restore stopped because the file already exists: {file.RelativePath}"); Directory.CreateDirectory(Path.GetDirectoryName(destination)!); File.Copy(Path.Combine(snapshotRoot, file.RelativePath), destination); } return snapshot.Files.Count; }
    private static string Hash(string path) { using var stream = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(stream)); }
}
