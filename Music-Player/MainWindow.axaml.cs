using System.Collections.ObjectModel;
using Avalonia.Controls;
using System.Text.Json;
using System.IO;
using System;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using System.Linq;
using PlaybackLibrary;

namespace Music_Player;

#region app settings
public class AppSettings
{
    public string LastOpenedFolderPath { get; set; } = "";
}

public class SettingsService
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MyApp",
        "settings.json");

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
                return new AppSettings();

            var json = File.ReadAllText(SettingsPath);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Settings Load Error: {ex.Message}");
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        var directory = Path.GetDirectoryName(SettingsPath)!;
        Directory.CreateDirectory(directory);

        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        File.WriteAllText(SettingsPath, json);
    }
}
#endregion

public class SongFile
{
    public string Name { get; set; } = "";
    public Uri Path { get; set; } = new Uri("about:blank");
    public int Index;
}

public static class ObservableCollectionExtension
{
    public static void Shuffle<T>(this ObservableCollection<T> list)
    {
        Random rng = new();
        int n = list.Count;
        while (n > 1)
        {
            n--;
            int k = rng.Next(n+1);
            (list[n], list[k]) = (list[k], list[n]);
        }
    }
}

public partial class MainWindow : Window
{
    private readonly SettingsService _settings;
    private readonly ISongPlayback _song = SongPlayback.Create();
    private bool _isPaused = true;
    public ObservableCollection<SongFile> OriginalPlaylist { get; } = [];
    public ObservableCollection<SongFile> Playlist { get; } = [];
    private bool IsShuffled = false;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
        _settings = new SettingsService();
        _ = LoadPlaylistAsync();
    }

    #region media playback functions
    public void LoadAndPlay(SongFile file)
    {
        CurrentSongLabel.Text = Path.GetFileName(file.Name);
        _song.Load(file.Index);
        _song.Play();
        _isPaused = false;
    }

    private void OnPlayButtonClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _isPaused = !_isPaused;
        if (_isPaused)
            _song.Pause();
        else
            _song.Play();
    }
    #endregion

    private async Task LoadPlaylistAsync()
    {
        var topLevel = GetTopLevel(this);
        if (topLevel == null)
        {
            Console.WriteLine("LoadPlaylist Error: top level is null");
            return;
        }

        AppSettings s = _settings.Load();
        IStorageFolder? folder = null;

        if (!string.IsNullOrWhiteSpace(s.LastOpenedFolderPath))
        {
            folder = await topLevel.StorageProvider.TryGetFolderFromPathAsync(
                new Uri(s.LastOpenedFolderPath));
        }

        if (folder == null)
        {
            string exePath = AppDomain.CurrentDomain.BaseDirectory;
            folder = await topLevel.StorageProvider.TryGetFolderFromPathAsync(new Uri(exePath));
            if (folder == null)
            {
                Console.WriteLine("Error: could not resolve a music folder.");
                return;
            }
        }
        OriginalPlaylist.Clear();
        Playlist.Clear();
        _song.ClearPlaylist();
        int songIndex = 0;
        await foreach (var item in folder.GetItemsAsync())
        {
            if (item is IStorageFile file &&
                file.Name.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase))
            {
                OriginalPlaylist.Add(new SongFile
                {
                    Name = file.Name,
                    Path = file.Path,
                    Index = songIndex
                });
                Playlist.Add(new SongFile
                {
                    Name = file.Name,
                    Path = file.Path,
                    Index = songIndex
                });
                _song.AddToPlaylist(file.Path);
                songIndex++;
            }
        }
        if (IsShuffled) Playlist.Shuffle();
    }

    private void OnPlaylistSongClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: SongFile song })
            return;

        LoadAndPlay(song);

        AppSettings s = _settings.Load();
        _settings.Save(s);
    }

    public async void OnOpenFolderButtonClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var topLevel = GetTopLevel(this);
        if (topLevel == null)
        {
            Console.WriteLine("OnOpenFolderButtonClick Error: top level is null");
            return;
        }

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select Folder",
            AllowMultiple = false
        });
        if (folders.Count <= 0) return;

        AppSettings s = _settings.Load();
        s.LastOpenedFolderPath = folders[0].Path.AbsoluteUri;
        _settings.Save(s);

        await LoadPlaylistAsync();
    }

    public async void ShuffleClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        IsShuffled = !IsShuffled;
        await LoadPlaylistAsync();
    }
}
