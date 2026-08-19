using Windows.Foundation;
using Windows.Media.Core;
using Windows.Media.Playback;

namespace PlaybackLibrary;

public interface ISongPlayback : IDisposable
{
    event Action? TrackEnded;
    TimeSpan Position { get; set; }
    void Load(int playlistIndex);
    Task<double> Play();
    void Resume();
    void Pause();
    void AddToPlaylist(Uri path);
    void ClearPlaylist();
}
 public static class SongPlayback
{
    public static ISongPlayback Create()
    {
#if WINDOWS
        return new WindowsSongPlayback();
#else
        return new StubSongPlayback();
#endif
    }
}

#if WINDOWS
file sealed class WindowsSongPlayback : ISongPlayback
{
    public event Action? TrackEnded;

    private MediaPlayer? _player;
    private readonly MediaPlaybackList _playbackList = new();
    private bool _ignoreTrackEnded;

    private MediaPlayer Player =>
        _player ??= new MediaPlayer();

    public WindowsSongPlayback()
    {
        _playbackList.CurrentItemChanged += OnCurrentItemChanged;
    }

    private void OnCurrentItemChanged(MediaPlaybackList sender, CurrentMediaPlaybackItemChangedEventArgs args)
    {
        if (_ignoreTrackEnded) return;
        if (args.Reason != MediaPlaybackItemChangedReason.EndOfStream) return;
        TrackEnded?.Invoke();
    }

    public TimeSpan Position
    {
        get => _player?.PlaybackSession.Position ?? TimeSpan.Zero;
        set
        {
            if (_player is null) return;
            Player.PlaybackSession.Position = value;
        }
    }

    public void Load(int playlistIndex)
    {
        if (playlistIndex < 0 || playlistIndex >= _playbackList.Items.Count)
            return;
        _ignoreTrackEnded = true;
        Player.Source = null;
        _playbackList.StartingItem = _playbackList.Items[playlistIndex];
        Player.Source = _playbackList;
    }

    public async Task<double> Play()
    {
        var tcs = new TaskCompletionSource<double>();
        void openedHandler(MediaPlayer sender, object args)
        {
            Player.MediaOpened -= openedHandler;
            double seconds = Player.PlaybackSession.NaturalDuration.TotalSeconds;
            tcs.SetResult(seconds);
        }

        Player.MediaOpened += openedHandler;
        Player.Play();
        double duration = await tcs.Task;
        _ignoreTrackEnded = false;
        return duration;
    }

    public void Resume() => Player.Play();

    public void Pause()
    {
        Player.Pause();
    }

    public void AddToPlaylist(Uri path)
    {
        Uri sourceUri = path.IsFile
            ? new Uri(Path.GetFullPath(path.LocalPath))
            : path;
        MediaSource mediaSource = MediaSource.CreateFromUri(sourceUri);
        _playbackList.Items.Add(new MediaPlaybackItem(mediaSource));
    }

    public void ClearPlaylist()
    {
        _ignoreTrackEnded = true;
        if (_player != null)
            _player.Source = null;
        _playbackList.Items.Clear();
        _playbackList.StartingItem = null;
        _ignoreTrackEnded = false;
    }

    public void Dispose()
    {
        _playbackList.CurrentItemChanged -= OnCurrentItemChanged;
        if (_player is null) return;
        _player.Pause();
        _player.Source = null;
        _player.Dispose();
        _player = null;
    }
}
#else
file sealed class StubSongPlayback : ISongPlayback
{
    public event Action? TrackEnded;
    public TimeSpan Position { get; set; }

    public void Load(int playlistIndex) =>
        Console.WriteLine($"Load: Linux version WOP");

    public Task<double> Play()
    {
        Console.WriteLine("Play: Linux version WOP");
        return Task.FromResult(0.0);
    }

    public void Resume() => Console.WriteLine("Resume: Linux version WOP");

    public void Pause() => Console.WriteLine("Pause: Linux version WOP");

    public void AddToPlaylist(Uri path)
    {
        
    }

    public void ClearPlaylist()
    {
        
    }

    public void Dispose() { }
}
#endif
