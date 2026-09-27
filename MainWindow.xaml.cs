using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Media.Animation;
using Forms = System.Windows.Forms;
namespace OpenGameSave;
public partial class MainWindow : Window
{
    private string? saveFolder; private string? library; private SaveSnapshot? selected;
    private List<DetectedGame> detectedGames = new();
    private System.Windows.Controls.Image? gameArtwork;
    private System.Windows.Controls.TextBlock? restorePlanText;
    private readonly System.Windows.Controls.ComboBox savedProfileSelector = new() { Width = 170, ToolTip = "Saved profiles" };
    public MainWindow()
    {
        InitializeComponent();
        Opacity = 0;
        Loaded += (_, _) => BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(260)));
        if (SelectedText.Parent is System.Windows.Controls.Panel details)
        {
            gameArtwork = new System.Windows.Controls.Image { Width = 280, Height = 105, Stretch = System.Windows.Media.Stretch.UniformToFill, HorizontalAlignment = System.Windows.HorizontalAlignment.Left, Margin = new Thickness(0, 7, 0, 12) };
            details.Children.Insert(details.Children.IndexOf(SelectedText), gameArtwork);
        }
        if (StatusText.Parent is System.Windows.Controls.Panel statusPanel)
        {
            restorePlanText = new System.Windows.Controls.TextBlock { Foreground = (System.Windows.Media.Brush)FindResource("Accent"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 0) };
            statusPanel.Children.Insert(statusPanel.Children.IndexOf(StatusText) + 1, restorePlanText);
        }
        GameSelector.SelectionChanged += (_, _) => { if (GameSelector.SelectedItem is DetectedGame game) ShowArtwork(game); };
        savedProfileSelector.ItemsSource = ProfileStore.Load(); savedProfileSelector.DisplayMemberPath = nameof(SavedProfile.Name);
        if (ProfileText.Parent is System.Windows.Controls.Panel profilePanel)
        {
            var load = new System.Windows.Controls.Button { Content = "Load profile", Padding = new Thickness(10, 6, 10, 6), Margin = new Thickness(12, 0, 0, 0) };
            load.Click += LoadProfile_Click; profilePanel.Children.Add(savedProfileSelector); profilePanel.Children.Add(load);
        }
        var saved = ProfileStore.Load().LastOrDefault(profile => Directory.Exists(profile.SaveFolder) && Directory.Exists(profile.Library));
        if (saved is not null)
        {
            saveFolder = saved.SaveFolder; library = saved.Library; ProfileText.Text = saved.Name; StatusText.Text = $"Ready · {saved.Name} profile loaded."; Refresh();
        }
    }
    private void LoadProfile_Click(object sender, RoutedEventArgs e)
    {
        if (savedProfileSelector.SelectedItem is not SavedProfile profile) { StatusText.Text = "Choose a saved profile first."; return; }
        saveFolder = profile.SaveFolder; library = profile.Library; ProfileText.Text = profile.Name; StatusText.Text = $"Ready · {profile.Name} profile loaded."; Refresh();
    }
    private void DetectSteam_Click(object sender, RoutedEventArgs e)
    {
        var roots = SteamDiscovery.FindSteamRoots();
        detectedGames = SteamDiscovery.Discover(roots).ToList();
        GameSelector.ItemsSource = detectedGames;
        if (detectedGames.Count == 0) StatusText.Text = roots.Count == 0 ? "Steam was not found. Install Steam or choose a save folder manually." : $"Steam was found in {roots.Count:N0} location(s), but no installed game manifests were found.";
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
    private async void Snapshot_Click(object sender, RoutedEventArgs e) { if (saveFolder is null || library is null) { StatusText.Text = "Choose a save folder and backup library first."; return; } var profile = new GameProfile(Path.GetFileName(saveFolder), null, saveFolder); SnapshotButton.IsEnabled = false; StatusText.Text = "Creating verified snapshot…"; try { selected = await Task.Run(() => SaveEngine.CreateSnapshot(profile, library)); ProfileStore.Save(new SavedProfile(profile.Name, profile.SaveFolder, library)); StatusText.Text = $"Snapshot verified: {selected.Files.Count:N0} files."; Refresh(); } catch (Exception ex) { StatusText.Text = ex.Message; } finally { SnapshotButton.IsEnabled = true; } }
    private void Refresh() { if (library is null || saveFolder is null) return; var name = Path.GetFileName(saveFolder); var root = Path.Combine(library, name); if (!Directory.Exists(root)) return; SnapshotList.ItemsSource = Directory.EnumerateDirectories(root).OrderByDescending(x => x).Select(Path.GetFileName).ToArray(); CountText.Text = $"{Directory.EnumerateDirectories(root).Count():N0} snapshots"; }
    private void Snapshot_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) { if (library is null || SnapshotList.SelectedItem is not string id || saveFolder is null) return; try { selected = SaveEngine.Load(Path.Combine(library, Path.GetFileName(saveFolder), id)); SelectedText.Text = id; StatusText.Text = $"{selected.Files.Count:N0} files · {FormatBytes(selected.TotalBytes)} · {selected.CreatedAt:g}"; var plan = SaveEngine.PlanRestore(Path.Combine(library, selected.ProfileName, selected.Id), saveFolder); if (restorePlanText is not null) restorePlanText.Text = plan.ExistingFiles == 0 ? $"Ready to restore · {plan.NewFiles:N0} files will be added." : $"Review required · {plan.ExistingFiles:N0} existing files will block restore."; } catch (Exception ex) { StatusText.Text = ex.Message; } }
    private static string FormatBytes(long bytes) => bytes switch { >= 1_000_000_000 => $"{bytes / 1_000_000_000d:0.0} GB", >= 1_000_000 => $"{bytes / 1_000_000d:0.0} MB", >= 1_000 => $"{bytes / 1_000d:0.0} KB", _ => $"{bytes} B" };
    private void Restore_Click(object sender, RoutedEventArgs e) { if (selected is null || library is null || saveFolder is null) { StatusText.Text = "Select a snapshot first."; return; } try { var count = SaveEngine.Restore(Path.Combine(library, selected.ProfileName, selected.Id), saveFolder); StatusText.Text = $"Restored {count:N0} files."; } catch (Exception ex) { StatusText.Text = ex.Message; } }
}
