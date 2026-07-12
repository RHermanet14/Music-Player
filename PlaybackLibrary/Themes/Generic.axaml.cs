using Avalonia.Markup.Xaml;
using Avalonia.Styling;

namespace PlaybackLibrary.Themes;

public class Generic : Styles
{
    public Generic()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
