using System.Collections.ObjectModel;
using Avalonia.Controls;
using System.Text.Json;
using System.IO;
using System;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using System.Linq;
using PlaybackLibrary;
using Avalonia.Data.Converters;
using System.Globalization;
using System.ComponentModel;
using Avalonia;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System.Collections.Generic;

namespace Music_Player;

#region app settings
public class AppSettings
{
    public string LastOpenedFolderPath { get; set; } = "";
}

public class SeekBarConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is double totalTime)
        {
            if (double.IsNaN(totalTime) || totalTime < 0) totalTime = 0;
            TimeSpan elapsed = TimeSpan.FromSeconds(Math.Floor(totalTime));
            return $"{(int)elapsed.TotalMinutes}:{elapsed.Seconds:00}";
        }
        return value;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value;
    }
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
    public int Index { get; set; }
    public DateTime DateCreated {get; set;}
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
    private static readonly DataFormat<SongFile> SongFileDragFormat =
        DataFormat.CreateInProcessFormat<SongFile>("MusicPlayer.SongFile");
    private const double PlaylistDragThreshold = 8;

    private readonly SettingsService _settings;
    private readonly ISongPlayback _song = SongPlayback.Create();
    private bool _isPaused = true;
    public ObservableCollection<SongFile> OriginalPlaylist { get; } = [];
    public ObservableCollection<SongFile> Playlist { get; } = [];
    public ObservableCollection<SongFile> FilteredPlaylist {get;} = [];
    public static readonly StyledProperty<bool> IsFilteredProperty =
        AvaloniaProperty.Register<MainWindow, bool>(nameof(IsFiltered));
    private bool IsShuffled = false;
    public static readonly StyledProperty<bool> IsSeekBarVisibleProperty =
        AvaloniaProperty.Register<MainWindow, bool>(nameof(IsSeekBarVisible));
    public static readonly StyledProperty<SongFile?> CurrentSongProperty =
        AvaloniaProperty.Register<MainWindow, SongFile?>(nameof(CurrentSong));

    public bool IsFiltered
    {
        get => GetValue(IsFilteredProperty);
        set => SetValue(IsFilteredProperty, value);
    }

    public bool IsSeekBarVisible
    {
        get => GetValue(IsSeekBarVisibleProperty);
        set => SetValue(IsSeekBarVisibleProperty, value);
    }

    public SongFile? CurrentSong
    {
        get => GetValue(CurrentSongProperty);
        set => SetValue(CurrentSongProperty, value);
    }

    public DispatcherTimer timer = new();
    private bool _isUserSeeking;
    private bool _playlistDragActive;
    private bool _playlistDidDrag;
    private SongFile? _clickPlaySong;
    private SongFile? _pendingDragSong;
    private Point? _dragStartPoint;
    private PointerPressedEventArgs? _dragPressEvent;
    private ListBox _playlistListBox = null!;

    public MainWindow()
    {
        InitializeComponent();
        _playlistListBox = this.FindControl<ListBox>("playlistListBox")!;
        DataContext = this;
        _settings = new SettingsService();
        _ = LoadPlaylistAsync();
        timer.Interval = TimeSpan.FromMilliseconds(250);
        timer.Tick += (_, _) => UpdateSeekBar();
        seekBarSlider.AddHandler(InputElement.PointerPressedEvent, OnSeekBarPointerPressed, RoutingStrategies.Tunnel);
        seekBarSlider.AddHandler(InputElement.PointerReleasedEvent, OnSeekBarPointerReleased, RoutingStrategies.Tunnel);
        seekBarSlider.AddHandler(InputElement.PointerCaptureLostEvent, OnSeekBarPointerCaptureLost, RoutingStrategies.Tunnel);
        _song.TrackEnded += () => Dispatcher.UIThread.Post(OnTrackEnded);
        sortComboBox.SelectionChanged += OnSortComboBoxChanged;
    }

    private void UpdateSeekBar()
    {
        if (_isUserSeeking) return;
        double seconds = _song.Position.TotalSeconds;
        if (double.IsNaN(seconds) || seconds < 0) seconds = 0;
        if (seconds > seekBarSlider.Maximum) seconds = seekBarSlider.Maximum;
        seekBarSlider.Value = seconds;
    }

    private void OnSeekBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _isUserSeeking = true;
    }

    private void OnSeekBarPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        CommitSeek();
    }

    private void OnSeekBarPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        CommitSeek();
    }

    private void CommitSeek()
    {
        if (!_isUserSeeking) return;
        _song.Position = TimeSpan.FromSeconds(seekBarSlider.Value);
        _isUserSeeking = false;
        UpdateSeekBar();
    }

    #region media playback functions
    public async void LoadAndPlay(SongFile file)
    {
        CurrentSong = file;
        _playlistListBox.SelectedItem = file;

        timer.Stop();
        CurrentSongLabel.Text = Path.GetFileName(file.Name);
        _song.Load(file.Index);
        seekBarSlider.Value = 0;
        seekBarSlider.Maximum = await _song.Play();
        _isPaused = false;
        IsSeekBarVisible = true;
        UpdateSeekBar();
        timer.Start();
    }

    private SongFile? SongAtOffset(int delta)
    {
        if (CurrentSong is null || Playlist.Count == 0)
            return null;

        int i = -1;
        for (int n = 0; n < Playlist.Count; n++)
        {
            if (Playlist[n].Index == CurrentSong.Index)
            {
                i = n;
                break;
            }
        }
        if (i < 0) return null;

        int next = i + delta;
        if (next < 0 || next >= Playlist.Count)
            return null;
        return Playlist[next];
    }

    private void OnPlaylistSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        // Playback is handled on pointer release so drag-to-reorder doesn't start playing.
    }

    private void OnPlaylistItemPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Border border || border.DataContext is not SongFile song) return;
        if (!e.GetCurrentPoint(border).Properties.IsLeftButtonPressed) return;

        _clickPlaySong = song;
        _playlistDidDrag = false;
        _pendingDragSong = song;
        _dragStartPoint = e.GetPosition(border);
        _dragPressEvent = e;
        e.Pointer.Capture(border);
    }

    private async void OnPlaylistItemPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_pendingDragSong is null || _dragStartPoint is null || _dragPressEvent is null || sender is not Border border) return;
        if (!e.GetCurrentPoint(border).Properties.IsLeftButtonPressed) return;

        var current = e.GetPosition(border);
        var dx = current.X - _dragStartPoint.Value.X;
        var dy = current.Y - _dragStartPoint.Value.Y;
        if (dx * dx + dy * dy < PlaylistDragThreshold * PlaylistDragThreshold) return;

        _playlistDidDrag = true;
        _clickPlaySong = null;
        var song = _pendingDragSong;
        var pressEvent = _dragPressEvent;
        ClearPlaylistDragState(border, e.Pointer);

        _playlistDragActive = true;
        var item = new DataTransferItem();
        item.Set(SongFileDragFormat, song);
        var transfer = new DataTransfer();
        transfer.Add(item);
        await DragDrop.DoDragDropAsync(pressEvent, transfer, DragDropEffects.Move);
        Dispatcher.UIThread.Post(() => _playlistDragActive = false);
    }

    private void OnPlaylistItemPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (sender is not Border border) return;

        bool shouldPlay = _clickPlaySong is not null && !_playlistDidDrag && !_playlistDragActive;
        SongFile? song = _clickPlaySong;
        ClearPlaylistDragState(border, e.Pointer);
        _clickPlaySong = null;
        _playlistDidDrag = false;

        if (shouldPlay && song is not null)
            LoadAndPlay(song);
    }

    private void ClearPlaylistDragState(Border border, IPointer pointer)
    {
        _pendingDragSong = null;
        _dragStartPoint = null;
        _dragPressEvent = null;
        if (pointer.Captured == border)
            pointer.Capture(null);
    }

    private void OnPlaylistDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.DataTransfer.Contains(SongFileDragFormat)
            ? DragDropEffects.Move
            : DragDropEffects.None;
    }

    private void OnPlaylistDrop(object? sender, DragEventArgs e)
    {
        if (e.DataTransfer.TryGetValue(SongFileDragFormat) is not SongFile dragged) return;

        var target = FindSongFileAtPoint(e.GetPosition(_playlistListBox));
        if (target is null || ReferenceEquals(dragged, target)) return;

        int oldIndex = Playlist.IndexOf(dragged);
        int newIndex = Playlist.IndexOf(target);
        if (oldIndex < 0 || newIndex < 0 || oldIndex == newIndex) return;

        Playlist.Move(oldIndex, newIndex);
    }

    private SongFile? FindSongFileAtPoint(Point position)
    {
        if (_playlistListBox.InputHitTest(position) is not Visual hit) return null;

        Visual? current = hit;
        while (current is not null)
        {
            if (current is Control { DataContext: SongFile song })
                return song;
            current = current.GetVisualParent();
        }
        return null;
    }

    private void OnTrackEnded()
    {
        SongFile? next = SongAtOffset(1);
        if (next is null)
        {
            _isPaused = true;
            timer.Stop();
            UpdateSeekBar();
            return;
        }
        LoadAndPlay(next);
    }

    private void OnPlayButtonClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_isPaused)
        {
            _isPaused = false;
            _song.Resume();
            timer.Start();
        }
        else
        {
            _isPaused = true;
            _song.Pause();
            timer.Stop();
        }
        UpdateSeekBar();
    }

    private void OnTimeSkipClick(object? sender, RoutedEventArgs e)
    {
        if(sender is Control c) {
            string? name = c.Name;
            if (name == "prevTime")
            {
                _song.Position = TimeSpan.FromSeconds(seekBarSlider.Value - 5);
                _isUserSeeking = false;
                UpdateSeekBar();
            } 
            else
            {
                _song.Position = TimeSpan.FromSeconds(seekBarSlider.Value + 5);
                _isUserSeeking = false;
                UpdateSeekBar();
            }
        }
    }

    private void OnSongSkipClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control { Name: string name })
            return;

        int delta = name == "prevSong" ? -1 : 1;
        SongFile? target = SongAtOffset(delta);
        if (target is null)
            return;
        LoadAndPlay(target);
    }
    #endregion

    private async Task<List<SongFile>> SortPlaylist(List<IStorageItem> files)
    {
        List<SongFile> sortedSongs= [];
        foreach (var item in files)
        {
            if (item is IStorageFile file &&
                file.Name.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase))
            {
                var fileProperties = await file.GetBasicPropertiesAsync();
                DateTimeOffset? fileCreationDate = fileProperties?.DateCreated;
                DateTime fileDateTime = DateTime.MinValue;
                if (fileCreationDate.HasValue)
                {
                    fileDateTime = fileCreationDate.Value.DateTime;
                }
                sortedSongs.Add(new SongFile
                {
                    Name = item.Name,
                    Path = item.Path,
                    DateCreated = fileDateTime
                });
            }
        }
        sortedSongs = sortComboBox.SelectedIndex switch
        {
            1 => [.. sortedSongs.OrderByDescending(f => f.DateCreated)],
            2 => [.. sortedSongs.OrderBy(f => f.Name)],
            3 => [.. sortedSongs.OrderByDescending(f => f.Name)],
            _ => [.. sortedSongs.OrderBy(f => f.DateCreated)],
        };
        return sortedSongs;
    }

    private async void OnSortComboBoxChanged(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await LoadPlaylistAsync();
    }

    private void FilterSearchBar()
    {
        string filter = searchBar.Text ?? "";
        FilteredPlaylist.Clear();
        IsFiltered = !string.IsNullOrWhiteSpace(filter);
        if (IsFiltered)
        {
            foreach (SongFile song in OriginalPlaylist.Where(s => s.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)))
            FilteredPlaylist.Add(song);
        }
        SongBrowser.ItemsSource = IsFiltered ? FilteredPlaylist : OriginalPlaylist;
    }

    private void OnSearchBarChanged(object? sender, RoutedEventArgs e)
    {
        FilterSearchBar();
    }

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
        var files = await folder.GetItemsAsync().ToListAsync();
        OriginalPlaylist.Clear();
        Playlist.Clear();
        _song.ClearPlaylist();
        CurrentSong = null;
        _playlistListBox.SelectedItem = null;
        timer.Stop();
        _isPaused = true;
        seekBarSlider.Value = 0;
        IsSeekBarVisible = false;
        int songIndex = 0;

        List<SongFile> songs = await SortPlaylist(files);
        foreach (SongFile song in songs)
        {
            OriginalPlaylist.Add(new SongFile
            {
                Name = song.Name,
                Path = song.Path,
                Index = songIndex,
                DateCreated = song.DateCreated
            });
            Playlist.Add(new SongFile
            {
                Name = song.Name,
                Path = song.Path,
                Index = songIndex,
                DateCreated = song.DateCreated
            });
            _song.AddToPlaylist(song.Path);
            songIndex++;
        }
        if (IsShuffled) Playlist.Shuffle();
        FilterSearchBar();
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
