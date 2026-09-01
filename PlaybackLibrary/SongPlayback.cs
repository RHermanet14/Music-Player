using Windows.Media.Core;
using Windows.Media.Playback;

namespace PlaybackLibrary;

public interface ISongPlayback : IDisposable
{
    event Action? TrackEnded;
    TimeSpan Position { get; set; }
    TimeSpan Duration { get; }
    double Volume { get; set; }
    bool IsTransitioning { get; }
    void Load(int playlistIndex);
    Task<double> Play();
    Task<double> StartCrossfadeAsync(int nextPlaylistIndex, TimeSpan startOffset, TimeSpan crossfadeDuration, CancellationToken ct);
    void CancelTransition();
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

    private readonly MediaPlayer _playerA = new();
    private readonly MediaPlayer _playerB = new();
    private MediaPlayer _activePlayer;
    private MediaPlayer _inactivePlayer;
    private readonly MediaPlaybackList _playbackList = new();
    private CancellationTokenSource? _transitionCts;
    private bool _isTransitioning;
    private double _masterVolume = 1;

    public double Volume
    {
        get => _masterVolume;
        set
        {
            _masterVolume = Math.Clamp(value, 0, 1);
            if (!_isTransitioning)
                _activePlayer.Volume = _masterVolume;
        }
    }

    public WindowsSongPlayback()
    {
        _activePlayer = _playerA;
        _inactivePlayer = _playerB;
        _playerA.MediaEnded += OnActiveMediaEnded;
        _playerB.MediaEnded += OnActiveMediaEnded;
    }

    private void OnActiveMediaEnded(MediaPlayer sender, object args)
    {
        if (sender != _activePlayer || _isTransitioning) return;
        TrackEnded?.Invoke();
    }

    public bool IsTransitioning => _isTransitioning;

    public TimeSpan Position
    {
        get => _activePlayer.PlaybackSession.Position;
        set => _activePlayer.PlaybackSession.Position = value;
    }

    public TimeSpan Duration =>
        _activePlayer.PlaybackSession.NaturalDuration;

    public void Load(int playlistIndex)
    {
        CancelTransition();
        SetPlayerSource(_activePlayer, playlistIndex);
    }

    public async Task<double> Play()
    {
        CancelTransition();
        _activePlayer.Volume = _masterVolume;
        _activePlayer.Play();
        return await WaitForDurationAsync(_activePlayer, CancellationToken.None);
    }

    public async Task<double> StartCrossfadeAsync(
        int nextPlaylistIndex,
        TimeSpan startOffset,
        TimeSpan crossfadeDuration,
        CancellationToken ct)
    {
        CancelTransition();
        _transitionCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var token = _transitionCts.Token;
        _isTransitioning = true;

        var outgoing = _activePlayer;
        var incoming = _inactivePlayer;

        try
        {
            incoming.Volume = 0;
            SetPlayerSource(incoming, nextPlaylistIndex);
            double duration = await WaitForDurationAsync(incoming, token);

            if (startOffset.TotalSeconds > duration)
                startOffset = TimeSpan.Zero;
            incoming.PlaybackSession.Position = startOffset;
            incoming.Pause();

            if (crossfadeDuration.TotalSeconds <= 0)
            {
                outgoing.Pause();
                outgoing.Source = null;
                outgoing.Volume = _masterVolume;
                incoming.Volume = _masterVolume;
                incoming.Play();
                SwapPlayers();
                return duration;
            }

            incoming.Volume = 0;
            incoming.Play();
            outgoing.Play();

            int steps = Math.Max(1, (int)(crossfadeDuration.TotalMilliseconds / 50));
            for (int i = 0; i <= steps; i++)
            {
                token.ThrowIfCancellationRequested();
                double t = (double)i / steps;
                outgoing.Volume = (1 - t) * _masterVolume;
                incoming.Volume = t * _masterVolume;
                await Task.Delay(50, token);
            }

            outgoing.Pause();
            outgoing.Source = null;
            outgoing.Volume = _masterVolume;
            incoming.Volume = _masterVolume;
            SwapPlayers();
            return duration;
        }
        finally
        {
            _isTransitioning = false;
            _transitionCts?.Dispose();
            _transitionCts = null;
        }
    }

    public void CancelTransition()
    {
        if (_transitionCts is not null)
        {
            _transitionCts.Cancel();
            _transitionCts.Dispose();
            _transitionCts = null;
        }

        if (!_isTransitioning) return;

        _inactivePlayer.Pause();
        _inactivePlayer.Source = null;
        _inactivePlayer.Volume = _masterVolume;
        _activePlayer.Volume = _masterVolume;
        _isTransitioning = false;
    }

    public void Resume() => _activePlayer.Play();

    public void Pause() => _activePlayer.Pause();

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
        CancelTransition();
        _playerA.Pause();
        _playerB.Pause();
        _playerA.Source = null;
        _playerB.Source = null;
        _playbackList.Items.Clear();
    }

    public void Dispose()
    {
        CancelTransition();
        _playerA.MediaEnded -= OnActiveMediaEnded;
        _playerB.MediaEnded -= OnActiveMediaEnded;
        _playerA.Pause();
        _playerB.Pause();
        _playerA.Source = null;
        _playerB.Source = null;
        _playerA.Dispose();
        _playerB.Dispose();
    }

    private void SwapPlayers()
    {
        (_activePlayer, _inactivePlayer) = (_inactivePlayer, _activePlayer);
    }

    private void SetPlayerSource(MediaPlayer player, int playlistIndex)
    {
        if (playlistIndex < 0 || playlistIndex >= _playbackList.Items.Count)
            return;
        player.Source = _playbackList.Items[playlistIndex];
    }

    private static async Task<double> WaitForDurationAsync(MediaPlayer player, CancellationToken ct)
    {
        double existing = player.PlaybackSession.NaturalDuration.TotalSeconds;
        if (existing > 0 && !double.IsInfinity(existing))
            return existing;

        var tcs = new TaskCompletionSource<double>();
        void openedHandler(MediaPlayer sender, object args)
        {
            player.MediaOpened -= openedHandler;
            tcs.TrySetResult(player.PlaybackSession.NaturalDuration.TotalSeconds);
        }

        player.MediaOpened += openedHandler;
        player.Play();

        await using var reg = ct.Register(() => tcs.TrySetCanceled(ct));
        return await tcs.Task;
    }
}
#else
file sealed class StubSongPlayback : ISongPlayback
{
    public event Action? TrackEnded;
    public TimeSpan Position { get; set; }
    public TimeSpan Duration => TimeSpan.Zero;
    public bool IsTransitioning => false;
    public double Volume { get; set; } = 1;

    public void Load(int playlistIndex) =>
        Console.WriteLine($"Load: Linux version WOP");

    public Task<double> Play()
    {
        Console.WriteLine("Play: Linux version WOP");
        return Task.FromResult(0.0);
    }

    public Task<double> StartCrossfadeAsync(int nextPlaylistIndex, TimeSpan startOffset, TimeSpan crossfadeDuration, CancellationToken ct) =>
        Play();

    public void CancelTransition() { }

    public void Resume() => Console.WriteLine("Resume: Linux version WOP");

    public void Pause() => Console.WriteLine("Pause: Linux version WOP");

    public void AddToPlaylist(Uri path) { }

    public void ClearPlaylist() { }

    public void Dispose() { }
}
#endif
