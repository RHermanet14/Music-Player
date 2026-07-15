using Avalonia.Markup.Xaml;
using Avalonia.Styling;
#if WINDOWS
    using Windows.Media.Playback;
#endif
namespace PlaybackLibrary.Themes;

public class Generic : Styles
{
    private bool isPaused = true;
    private MediaPlayer? song;
    public Generic()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void ClearSong()
    {
        if (song == null) return;
        #if WINDOWS
            song.Close();
        #else
            Console.WriteLine("ClearSong: Linux version WOP");
        #endif
    }

    public void LoadSong(string fileName)
    {
        if (song == null) return;
        #if WINDOWS
            song.Open(new Uri($"{fileName}"));
        #else
            Console.WriteLine("LoadSong: Linux version WOP");
        #endif
    }

    public void Play(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        // Plays or pauses media playback
        if (song == null) return;
        isPaused = !isPaused;
        if (isPaused)
        {
            #if WINDOWS
                song.Pause();
            #else
                Console.WriteLine("Play: Linux version WOP");
            #endif
        } else
        {
            #if WINDOWS
                song.Play();
            #else
                Console.WriteLine("Pause: Linux version WOP");
            #endif
        }
    }
}
