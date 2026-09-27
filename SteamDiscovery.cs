using Microsoft.Win32;
using System.IO;
using System.Text.RegularExpressions;
namespace OpenGameSave;
public sealed record DetectedGame(string AppId, string Name, string InstallFolder, string LibraryPath);
public static class SteamDiscovery
{
    public static IReadOnlyList<string> FindSteamRoots()
    {
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddRoot(roots, Environment.GetEnvironmentVariable("STEAM_PATH"));
        AddRoot(roots, Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null) as string);
        AddRoot(roots, Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath", null) as string);
        AddRoot(roots, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam"));
        AddRoot(roots, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Steam"));
        return roots.ToArray();
    }
    public static IReadOnlyList<DetectedGame> Discover(IEnumerable<string> steamRoots)
    {
        var results = new List<DetectedGame>();
        foreach (var root in steamRoots.Where(Directory.Exists))
        {
            var libraries = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { root };
            var manifest = Path.Combine(root, "steamapps", "libraryfolders.vdf");
            if (File.Exists(manifest)) foreach (Match match in Regex.Matches(File.ReadAllText(manifest), "\\\"path\\\"\\s+\\\"([^\\\"]+)\\\"")) AddRoot(libraries, match.Groups[1].Value.Replace("\\\\", "\\"));
            foreach (var library in libraries)
            {
                var apps = Path.Combine(library, "steamapps"); if (!Directory.Exists(apps)) continue;
                foreach (var file in Directory.EnumerateFiles(apps, "appmanifest_*.acf"))
                {
                    var text = File.ReadAllText(file); var name = Value(text, "name"); var folder = Value(text, "installdir"); var id = Path.GetFileNameWithoutExtension(file).Replace("appmanifest_", "");
                    if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(folder)) results.Add(new DetectedGame(id, name, Path.Combine(library, "steamapps", "common", folder), library));
                }
            }
        }
        return results.GroupBy(x => x.AppId).Select(x => x.First()).OrderBy(x => x.Name).ToArray();
    }
    private static string Value(string text, string key) => Regex.Match(text, $"\\\"{key}\\\"\\s+\\\"([^\\\"]+)\\\"").Groups[1].Value;
    private static void AddRoot(ISet<string> roots, string? root) { if (!string.IsNullOrWhiteSpace(root) && Directory.Exists(root)) roots.Add(root); }
}
