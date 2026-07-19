using System.Collections.ObjectModel;
using Avalonia.Controls;
using System.Text.Json;
using System.IO;
using System;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using System.Collections.Generic;

namespace Music_Player;

#region app settings
public class AppSettings
{
    public IStorageFolder? LastOpenedFolder {get; set;}
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

public class SongFile
{
    public string Name {get; set;} = "";
    public Uri Path {get;set;} = new Uri("");
}

public partial class MainWindow : Window
{
    private readonly SettingsService _settings;
    private readonly List<SongFile> SongList = []; // why does it suggest to make readonly?
    public ObservableCollection<SongFile> Playlist {get; set;} = [];
    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
        _settings = new SettingsService();
        LoadPlaylist();
    }

    private async void LoadPlaylist()
    {
        AppSettings s = _settings.Load();
        if (s.LastOpenedFolder == null)
        {
            var topLevel = GetTopLevel(this);
            if (topLevel == null)
            {
                Console.WriteLine("LoadPlaylist Error: top level is null");
                return;
            }
            string exePath = AppDomain.CurrentDomain.BaseDirectory;
            Uri folderUri = new(exePath);
            IStorageFolder? appFolder = await topLevel.StorageProvider.TryGetFolderFromPathAsync(folderUri);
           if (appFolder == null) 
           {
                Console.WriteLine("Error: LastOpenedFolder is empty and could not access current folder.");
                return;
            }
            s.LastOpenedFolder = appFolder;
        }
        await foreach(var item in s.LastOpenedFolder.GetItemsAsync())
        {
            if (item is IStorageFile file)
            {
                if (file.Name.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase))
                {
                    await using var stream = await file.OpenReadAsync();
                    using var reader = new StreamReader(stream);
                    string content = await reader.ReadToEndAsync();
                    SongFile newSong = new()
                    {
                        Name = file.Name,
                        Path = file.Path
                    };
                    SongList.Add(newSong);
                }
            }
        }
        Playlist.Clear();
        foreach (var song in SongList)
            Playlist.Add(song);
    }

    private void OnPlaylistSongClick()
    {
        //Get name of file
        //send file name to media player
        // MediaPlayer.SetSong(s.LastOpenedSong);
    }

    public async void OnOpenFolderButtonClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        // 1. open file browser
        // 2. check if selected folder is valid
        // 3. save selected folder to s.LastOpenedFolder
        // 4. LoadPlaylist();
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
        s.LastOpenedFolder = folders[0];
        SongList.Clear();
        LoadPlaylist();
    }
}