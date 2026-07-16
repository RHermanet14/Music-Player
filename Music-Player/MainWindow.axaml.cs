using System.Collections.ObjectModel;
using Avalonia.Controls;
using System.Text.Json;
using System.IO;
using System;

namespace Music_Player;

#region app settings
public class AppSettings
{
    public string LastOpenedFolder {get; set;} = "";
    public string LastOpenedFile { get; set; } = "";
}

public class SettingsService
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MyApp",
        "settings.json");

    // From the documentation: "Consider wrapping your Load method in a try/catch block. If the JSON file becomes corrupt or
    // its schema changes between application versions, JsonSerializer.Deserialize will throw an exception. Returning a default
    // AppSettings instance in the catch block prevents your application from crashing on startup." <-- TODO
    public AppSettings Load()
    {
        if (!File.Exists(SettingsPath))
            return new AppSettings();

        var json = File.ReadAllText(SettingsPath);
        return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
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

public partial class MainWindow : Window
{
    private readonly SettingsService _settings;
    public ObservableCollection<string>? Playlist {get; set;}
    public MainWindow()
    {
        InitializeComponent();
        _settings = new SettingsService();
        LoadPlaylist();
    }

    private void LoadPlaylist()
    {
        AppSettings s = _settings.Load();
        string directory = s.LastOpenedFolder;
        Playlist = new ObservableCollection<string>
        {
            // read all files in current directory (StorageProvider API)
        };
        // MediaPlayer.SetSong(s.LastOpenedSong);
    }

    private void OnPlaylistSongClick()
    {
        //Get name of file
        //send file name to media player
        // MediaPlayer.SetSong(s.LastOpenedSong);
    }

    public void OnOpenFolderButtonClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        // 1. open file browser
        // 2. check if selected folder is valid
        // 3. save selected folder to s.LastOpenedFolder
        // 4. LoadPlaylist();
    }
}