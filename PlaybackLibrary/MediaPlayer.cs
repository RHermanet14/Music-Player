using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;

namespace PlaybackLibrary;

public class MediaPlayer : TemplatedControl
{
    private const string PlayButtonPartName = "PART_PlayButton";

    private readonly ISongPlayback _song = SongPlayback.Create();
    private Button? _playButton;
    private bool _isPaused = true;

    public static readonly StyledProperty<string> FileProperty =
        AvaloniaProperty.Register<MediaPlayer, string>(nameof(File), defaultValue: "");

    public static readonly StyledProperty<bool> IsLeftCollapsedProperty =
        AvaloniaProperty.Register<MediaPlayer, bool>(nameof(IsLeftCollapsed));
    public bool IsLeftCollapsed
    {
        get => GetValue(IsLeftCollapsedProperty);
        set => SetValue(IsLeftCollapsedProperty, value);
    }

    public static readonly StyledProperty<bool> IsRightCollapsedProperty =
        AvaloniaProperty.Register<MediaPlayer, bool>(nameof(IsRightCollapsed));
    public bool IsRightCollapsed
    {
        get => GetValue(IsRightCollapsedProperty);
        set => SetValue(IsRightCollapsedProperty, value);
    }

    public string File
    {
        get => GetValue(FileProperty);
        set => SetValue(FileProperty, value);
    }

    public void LoadAndPlay(string fileName)
    {
        File = System.IO.Path.GetFileName(fileName);
        _song.Load(fileName);
        _song.Play();
        _isPaused = false;
    }

    public void TogglePlayPause()
    {
        _isPaused = !_isPaused;
        if (_isPaused)
            _song.Pause();
        else
            _song.Play();
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (_playButton is not null)
            _playButton.Click -= OnPlayButtonClick;

        _playButton = e.NameScope.Find<Button>(PlayButtonPartName);
        if (_playButton is not null)
            _playButton.Click += OnPlayButtonClick;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_playButton is not null)
        {
            _playButton.Click -= OnPlayButtonClick;
            _playButton = null;
        }

        base.OnDetachedFromVisualTree(e);
    }

    private void OnPlayButtonClick(object? sender, RoutedEventArgs e) => TogglePlayPause();
}
