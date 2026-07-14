using Avalonia.Markup.Xaml;
using Avalonia.Styling;

namespace PlaybackLibrary.Themes;

public class Generic : Styles
{
    private bool isPaused = true;
    public Generic()
    {
        AvaloniaXamlLoader.Load(this);
    }
    
    public void Play(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        // Plays or pauses media playback
        isPaused = !isPaused;
        if (isPaused)
        {
            
        } else
        {
            
        }
    }
}
