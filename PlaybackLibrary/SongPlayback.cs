using Windows.Media.Core;
using Windows.Media.Playback;

namespace PlaybackLibrary;

public interface ISongPlayback : IDisposable
{
    void Load(string fileName);
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
    private MediaPlaybackList _playbackList;

    private MediaPlayer Player =>
        _player ??= new MediaPlayer();

    public void Load(string fileName)
    { // switch to accessing index of _playbackList
        var uri = new Uri(Path.GetFullPath(fileName));
        Player.Source = MediaSource.CreateFromUri(uri);
    }

    public void Play() => Player.Play();

    public void Pause() => Player.Pause();

    public void AddToPlaylist(Uri path)
    {
        MediaSource mediaSource = MediaSource.CreateFromUri(path); // possibly need to get full path
        MediaPlaybackItem playbackItem = new(mediaSource);
        _playbackList.Items.Add(playbackItem);
    }

    public void ClearPlaylist()
    {
        _playbackList.Items.Clear();
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
    public void Load(string fileName) =>
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
