using Avalonia.Markup.Xaml;
using Avalonia.Styling;

namespace PlaybackLibrary.Themes;

public class Generic : Styles
{
    public bool IsLeftCollapsed, IsRightCollapsed;
    public Generic()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
