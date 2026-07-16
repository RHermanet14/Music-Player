namespace PlaybackLibrary;
internal interface ISongPlayback : IDisposable
{
    void Load(string fileName);
    void Play();
    void Pause();
}

internal static class SongPlayback
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
internal sealed class WindowsSongPlayback : ISongPlayback
{
    private Windows.Media.Playback.MediaPlayer? _player;

    private Windows.Media.Playback.MediaPlayer Player =>
        _player ??= new Windows.Media.Playback.MediaPlayer();

    public void Load(string fileName)
    {
        var uri = new Uri(Path.GetFullPath(fileName));
        Player.Source = Windows.Media.Core.MediaSource.CreateFromUri(uri);
    }

    public void Play() => Player.Play();

    public void Pause() => Player.Pause();

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
internal sealed class StubSongPlayback : ISongPlayback
{
    public void Load(string fileName) =>
        Console.WriteLine($"LoadSong: non-Windows stub ({fileName})");

    public void Play() => Console.WriteLine("Play: non-Windows stub");

    public void Pause() => Console.WriteLine("Pause: non-Windows stub");

    public void Dispose() { }
}
#endif
