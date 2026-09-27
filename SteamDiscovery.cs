using System.IO;
using System.Text.RegularExpressions;
namespace OpenGameSave;
public sealed record DetectedGame(string AppId, string Name, string InstallFolder, string LibraryPath);
public static class SteamDiscovery
{
    public static IReadOnlyList<DetectedGame> Discover(IEnumerable<string> steamRoots)
    {
        var results = new List<DetectedGame>();
        foreach (var root in steamRoots.Where(Directory.Exists)) { var manifest = Path.Combine(root, "steamapps", "libraryfolders.vdf"); var libraries = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { root }; if (File.Exists(manifest)) foreach (Match match in Regex.Matches(File.ReadAllText(manifest), "\\\"path\\\"\\s+\\\"([^\\\"]+)\\\"")) libraries.Add(match.Groups[1].Value.Replace("\\\\", "\\")); foreach (var library in libraries) { var apps = Path.Combine(library, "steamapps"); if (!Directory.Exists(apps)) continue; foreach (var file in Directory.EnumerateFiles(apps, "appmanifest_*.acf")) { var text = File.ReadAllText(file); var name = Regex.Match(text, "\\\"name\\\"\\s+\\\"([^\\\"]+)\\\"").Groups[1].Value; var folder = Regex.Match(text, "\\\"installdir\\\"\\s+\\\"([^\\\"]+)\\\"").Groups[1].Value; var id = Path.GetFileNameWithoutExtension(file).Replace("appmanifest_", ""); if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(folder)) results.Add(new DetectedGame(id, name, Path.Combine(library, "steamapps", "common", folder), library)); } } }
        return results.GroupBy(x => x.AppId).Select(x => x.First()).OrderBy(x => x.Name).ToArray();
    }
}
