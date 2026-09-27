using System.IO;
using System.Text.Json;
namespace OpenGameSave;
public sealed record SavedProfile(string Name, string SaveFolder, string Library);
public static class ProfileStore
{
    private static string FilePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenGameSave", "profiles.json");
    public static IReadOnlyList<SavedProfile> Load()
    {
        if (!File.Exists(FilePath)) return Array.Empty<SavedProfile>();
        try { return JsonSerializer.Deserialize<List<SavedProfile>>(File.ReadAllText(FilePath)) ?? new(); } catch { return Array.Empty<SavedProfile>(); }
    }
    public static void Save(SavedProfile profile)
    {
        var all = Load().Where(x => !x.Name.Equals(profile.Name, StringComparison.OrdinalIgnoreCase)).Append(profile).OrderBy(x => x.Name).ToList();
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!); File.WriteAllText(FilePath, JsonSerializer.Serialize(all, new JsonSerializerOptions { WriteIndented = true }));
    }
    public static SavedProfile? Find(string name) => Load().FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
}
