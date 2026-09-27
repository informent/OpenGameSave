using System.IO;
namespace OpenGameSave;
public static class SaveLocationLocator
{
    public static IReadOnlyList<string> FindCandidates(DetectedGame game, IEnumerable<string> roots)
    {
        var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase); var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments); var roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData); var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        foreach (var path in new[] { Path.Combine(documents, "My Games", game.Name), Path.Combine(documents, game.Name), Path.Combine(roaming, game.Name), Path.Combine(local, game.Name), Path.Combine(game.LibraryPath, "steamapps", "userdata") }) if (Directory.Exists(path)) candidates.Add(path);
        foreach (var root in roots.Where(Directory.Exists))
        {
            var direct = Path.Combine(root, game.AppId);
            if (Directory.Exists(direct)) candidates.Add(direct);
            foreach (var user in Directory.EnumerateDirectories(root))
            {
                var candidate = Path.Combine(user, game.AppId);
                if (Directory.Exists(candidate)) candidates.Add(candidate);
            }
        }
        return candidates.OrderBy(x => x).ToArray();
    }
}
