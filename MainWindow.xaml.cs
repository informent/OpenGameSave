using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using Forms = System.Windows.Forms;
namespace OpenGameSave;
public partial class MainWindow : Window
{
    private string? saveFolder; private string? library; private SaveSnapshot? selected;
    private List<DetectedGame> detectedGames = new();
    private System.Windows.Controls.Image? gameArtwork;
    public MainWindow()
    {
        InitializeComponent();
        if (SelectedText.Parent is System.Windows.Controls.Panel details)
        {
            gameArtwork = new System.Windows.Controls.Image { Width = 280, Height = 105, Stretch = System.Windows.Media.Stretch.UniformToFill, HorizontalAlignment = System.Windows.HorizontalAlignment.Left, Margin = new Thickness(0, 7, 0, 12) };
            details.Children.Insert(details.Children.IndexOf(SelectedText), gameArtwork);
        }
        GameSelector.SelectionChanged += (_, _) => { if (GameSelector.SelectedItem is DetectedGame game) ShowArtwork(game); };
    }
    private void DetectSteam_Click(object sender, RoutedEventArgs e)
    {
        var roots = new[] { Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam"), Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Steam") };
        detectedGames = SteamDiscovery.Discover(roots).ToList();
        GameSelector.ItemsSource = detectedGames;
        if (detectedGames.Count == 0) StatusText.Text = "No installed Steam games were found.";
        else { GameSelector.SelectedIndex = 0; ShowArtwork(detectedGames[0]); StatusText.Text = $"Detected {detectedGames.Count:N0} Steam games. Select one, then find its save folder."; }
    }
    private void ShowArtwork(DetectedGame game)
    {
        if (gameArtwork is null) return;
        try { gameArtwork.Source = new BitmapImage(new Uri($"https://cdn.cloudflare.steamstatic.com/steam/apps/{game.AppId}/header.jpg")); }
        catch { gameArtwork.Source = null; }
    }
    private void LocateSave_Click(object sender, RoutedEventArgs e)
    {
        if (GameSelector.SelectedItem is not DetectedGame game) { StatusText.Text = "Detect Steam games and select a game first."; return; }
        var userdata = Path.Combine(game.LibraryPath, "steamapps", "userdata");
        var candidates = SaveLocationLocator.FindCandidates(game, new[] { userdata });
        if (candidates.Count == 0) { StatusText.Text = "No likely save folder was found. Choose it manually."; return; }
        if (candidates.Count == 1) { saveFolder = candidates[0]; ProfileText.Text = game.Name; ShowArtwork(game); StatusText.Text = $"Found save folder: {saveFolder}"; Refresh(); return; }
        StatusText.Text = $"Found {candidates.Count:N0} possible save folders. Choose the exact folder manually.";
    }
    private void ChooseSave_Click(object sender, RoutedEventArgs e) { using var d = new Forms.FolderBrowserDialog { Description = "Choose this game's save folder" }; if (d.ShowDialog() == Forms.DialogResult.OK) { saveFolder = d.SelectedPath; ProfileText.Text = Path.GetFileName(saveFolder); StatusText.Text = "Choose a backup library."; Refresh(); } }
    private void ChooseLibrary_Click(object sender, RoutedEventArgs e) { using var d = new Forms.FolderBrowserDialog { Description = "Choose where OpenGameSave stores snapshots" }; if (d.ShowDialog() == Forms.DialogResult.OK) { library = d.SelectedPath; StatusText.Text = "Ready to create a snapshot."; Refresh(); } }
    private void Snapshot_Click(object sender, RoutedEventArgs e) { if (saveFolder is null || library is null) { StatusText.Text = "Choose a save folder and backup library first."; return; } try { selected = SaveEngine.CreateSnapshot(new GameProfile(Path.GetFileName(saveFolder), null, saveFolder), library); StatusText.Text = $"Snapshot verified: {selected.Files.Count:N0} files."; Refresh(); } catch (Exception ex) { StatusText.Text = ex.Message; } }
    private void Refresh() { if (library is null || saveFolder is null) return; var name = Path.GetFileName(saveFolder); var root = Path.Combine(library, name); if (!Directory.Exists(root)) return; SnapshotList.ItemsSource = Directory.EnumerateDirectories(root).OrderByDescending(x => x).Select(Path.GetFileName).ToArray(); CountText.Text = $"{Directory.EnumerateDirectories(root).Count():N0} snapshots"; }
    private void Snapshot_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) { if (library is null || SnapshotList.SelectedItem is not string id || saveFolder is null) return; try { selected = SaveEngine.Load(Path.Combine(library, Path.GetFileName(saveFolder), id)); SelectedText.Text = id; StatusText.Text = $"{selected.Files.Count:N0} files · {selected.CreatedAt:g}"; } catch (Exception ex) { StatusText.Text = ex.Message; } }
    private void Restore_Click(object sender, RoutedEventArgs e) { if (selected is null || library is null || saveFolder is null) { StatusText.Text = "Select a snapshot first."; return; } try { var count = SaveEngine.Restore(Path.Combine(library, selected.ProfileName, selected.Id), saveFolder); StatusText.Text = $"Restored {count:N0} files."; } catch (Exception ex) { StatusText.Text = ex.Message; } }
}
