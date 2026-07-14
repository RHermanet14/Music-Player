using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace PlaybackLibrary;

public class MediaPlayer : TemplatedControl
{
    
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<MediaPlayer, string>(nameof(Text));

    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }
}