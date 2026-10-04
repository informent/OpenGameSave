using System.IO;
using System.Security.Cryptography;
using System.Text.Json;

namespace OpenGameSave;

public sealed record GameProfile(string Name, string? GameFolder, string SaveFolder);
public sealed record SaveFile(string RelativePath, long Bytes, string Sha256);
public sealed record SaveSnapshot(string Id, DateTime CreatedAt, string ProfileName, string SaveFolder, IReadOnlyList<SaveFile> Files) { public long TotalBytes => Files.Sum(file => file.Bytes); }
public sealed record RestorePlan(int TotalFiles, int NewFiles, int ExistingFiles);

public static class SaveEngine
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static SaveSnapshot CreateSnapshot(GameProfile profile, string library)
    {
        if (!Directory.Exists(profile.SaveFolder)) throw new DirectoryNotFoundException(profile.SaveFolder);
        if (string.IsNullOrWhiteSpace(profile.Name) || profile.Name is "." or ".." || profile.Name != Path.GetFileName(profile.Name) || profile.Name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) throw new ArgumentException("Profile name must be a single safe folder name.", nameof(profile));
        var saveRoot = NormalizeDirectory(profile.SaveFolder);
        var libraryRoot = Path.GetFullPath(library);
        var profileRoot = Path.GetFullPath(Path.Combine(libraryRoot, profile.Name));
        if (IsWithinOrEqual(profileRoot, saveRoot)) throw new InvalidOperationException("The snapshot library cannot be inside the save folder.");
        Directory.CreateDirectory(profileRoot);
        var id = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff"); var finalRoot = Path.Combine(profileRoot, id);
        while (Directory.Exists(finalRoot)) { id = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + "-" + Random.Shared.Next(100, 999); finalRoot = Path.Combine(profileRoot, id); }
        var workingRoot = Path.Combine(profileRoot, $".opengamesave-{Guid.NewGuid():N}.partial"); Directory.CreateDirectory(workingRoot);
        try
        {
            var files = new List<SaveFile>();
            foreach (var file in EnumerateRegularFiles(saveRoot))
            {
                var relative = Path.GetRelativePath(saveRoot, file); EnsureSafeRelativePath(relative);
                var target = Resolve(workingRoot, relative); Directory.CreateDirectory(Path.GetDirectoryName(target)!); File.Copy(file, target, false);
                var copied = new FileInfo(target); files.Add(new SaveFile(relative, copied.Length, Hash(target)));
            }
            var snapshot = new SaveSnapshot(id, DateTime.Now, profile.Name, profile.SaveFolder, files.OrderBy(x => x.RelativePath, StringComparer.OrdinalIgnoreCase).ToArray());
            File.WriteAllText(Path.Combine(workingRoot, "manifest.json"), JsonSerializer.Serialize(snapshot, JsonOptions)); Directory.Move(workingRoot, finalRoot); return snapshot;
        }
        catch { if (Directory.Exists(workingRoot)) Directory.Delete(workingRoot, true); throw; }
    }

    public static bool Verify(string snapshotRoot)
    {
        var snapshot = Load(snapshotRoot); var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in snapshot.Files)
        {
            EnsureSafeRelativePath(file.RelativePath);
            if (!seen.Add(file.RelativePath)) throw new InvalidDataException($"Snapshot contains a duplicate path: {file.RelativePath}");
            if (file.Bytes < 0 || file.Sha256.Length != 64 || !file.Sha256.All(Uri.IsHexDigit)) throw new InvalidDataException($"Snapshot metadata is invalid: {file.RelativePath}");
            var path = Resolve(snapshotRoot, file.RelativePath); EnsureNoReparseAncestors(snapshotRoot, path);
            if (!File.Exists(path) || new FileInfo(path).Length != file.Bytes || !Hash(path).Equals(file.Sha256, StringComparison.OrdinalIgnoreCase)) return false;
        }
        return true;
    }

    public static SaveSnapshot Load(string root)
    {
        var manifest = Path.Combine(root, "manifest.json"); if (!File.Exists(manifest)) throw new FileNotFoundException("Snapshot manifest not found.", manifest);
        return JsonSerializer.Deserialize<SaveSnapshot>(File.ReadAllText(manifest)) ?? throw new InvalidDataException("Invalid save snapshot manifest.");
    }

    public static RestorePlan PlanRestore(string snapshotRoot, string target)
    {
        if (!Verify(snapshotRoot)) throw new InvalidDataException("Snapshot integrity verification failed.");
        var snapshot = Load(snapshotRoot); var existing = 0;
        foreach (var file in snapshot.Files) { var destination = Resolve(target, file.RelativePath); EnsureNoReparseAncestors(target, destination); if (File.Exists(destination)) existing++; }
        return new RestorePlan(snapshot.Files.Count, snapshot.Files.Count - existing, existing);
    }

    public static int Restore(string snapshotRoot, string target)
    {
        var plan = PlanRestore(snapshotRoot, target);
        if (plan.ExistingFiles > 0) throw new IOException($"Restore stopped before copying because {plan.ExistingFiles} destination file(s) already exist.");
        var snapshot = Load(snapshotRoot); Directory.CreateDirectory(target);
        foreach (var file in snapshot.Files) { var destination = Resolve(target, file.RelativePath); EnsureNoReparseAncestors(target, destination); Directory.CreateDirectory(Path.GetDirectoryName(destination)!); File.Copy(Resolve(snapshotRoot, file.RelativePath), destination, false); }
        return snapshot.Files.Count;
    }

    private static string Resolve(string root, string relative) { EnsureSafeRelativePath(relative); var basePath = NormalizeDirectory(root); var full = Path.GetFullPath(Path.Combine(basePath, relative)); if (!full.StartsWith(basePath, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Snapshot contains an unsafe path."); return full; }
    private static string NormalizeDirectory(string path) => Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
    private static bool IsWithinOrEqual(string candidate, string root) => NormalizeDirectory(candidate).StartsWith(NormalizeDirectory(root), StringComparison.OrdinalIgnoreCase);
    private static void EnsureSafeRelativePath(string relative) { if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative) || relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any(x => x is "" or "." or "..")) throw new InvalidDataException("Snapshot contains an unsafe path."); }
    private static IEnumerable<string> EnumerateRegularFiles(string root)
    {
        var pending = new Stack<string>(); pending.Push(root);
        while (pending.Count > 0)
        {
            var directory = pending.Pop(); if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException($"Snapshot stopped at reparse-point directory: {Path.GetRelativePath(root, directory)}");
            foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
            {
                var attributes = File.GetAttributes(entry);
                if ((attributes & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException($"Snapshot stopped at reparse point: {Path.GetRelativePath(root, entry)}");
                if ((attributes & FileAttributes.Directory) != 0) pending.Push(entry); else yield return entry;
            }
        }
    }
    private static void EnsureNoReparseAncestors(string root, string path)
    {
        var rootPath = NormalizeDirectory(root); var full = Path.GetFullPath(path);
        if (!full.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Resolved path escaped its root.");
        var current = Path.TrimEndingDirectorySeparator(rootPath);
        if (Directory.Exists(current) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Linked root directories are not allowed.");
        var relative = Path.GetRelativePath(current, full); var parts = relative.Split(Path.DirectorySeparatorChar);
        for (var i = 0; i < parts.Length - 1; i++) { current = Path.Combine(current, parts[i]); if (Directory.Exists(current) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException($"Linked directory is not allowed: {parts[i]}"); }
        if (File.Exists(full) && (File.GetAttributes(full) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Linked files are not allowed.");
    }
    private static string Hash(string path) { using var stream = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(stream)); }
}
