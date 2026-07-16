using Avalonia.Markup.Xaml;
using Avalonia.Styling;

namespace PlaybackLibrary.Themes;

public class Generic : Styles
{
    private bool isPaused = true;
    private readonly ISongPlayback song = SongPlayback.Create();

    public Generic()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void ClearSong()
    {
        song.Dispose();
    }

    public void LoadSong(string fileName)
    {
        song.Load(fileName);
    }

    public void Play(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        isPaused = !isPaused;
        if (isPaused)
            song.Pause();
        else
            song.Play();
    }
}
