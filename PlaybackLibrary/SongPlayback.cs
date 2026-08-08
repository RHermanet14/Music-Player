using Windows.Media.Core;
using Windows.Media.Playback;

namespace PlaybackLibrary;

public interface ISongPlayback : IDisposable
{
    void Load(int playlistIndex);
    void Play();
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
    private MediaPlayer? _player;
    private readonly MediaPlaybackList _playbackList = new();

    private MediaPlayer Player =>
        _player ??= new MediaPlayer();

    public void Load(int playlistIndex)
    {
        if (playlistIndex < 0 || playlistIndex >= _playbackList.Items.Count)
            return;
        Player.Source = null;
        _playbackList.StartingItem = _playbackList.Items[playlistIndex];
        Player.Source = _playbackList;
    }

    public void Play() => Player.Play();

    public void Pause() => Player.Pause();

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
        if (_player is not null)
            _player.Source = null;
        _playbackList.Items.Clear();
        _playbackList.StartingItem = null;
    }

    public void Dispose()
    {
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
    public void Load(int playlistIndex) =>
        Console.WriteLine($"Load: Linux version WOP");

    public void Play() => Console.WriteLine("Play: Linux version WOP");

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
