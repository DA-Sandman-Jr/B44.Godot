using System;
using Godot;

namespace B44.Godot.Display;

/// <summary>
/// Switches one window between fullscreen and its windowed mode. Entering fullscreen remembers whether the
/// window was windowed or maximized and, when windowed, where it was; leaving fullscreen goes back to that,
/// with the bounds placed by <see cref="WindowPlacement.Restore"/> so they fit the usable screen.
/// </summary>
/// <remarks>
/// The game owns the policy around it: the default, persisting the preference, and the control that toggles
/// it. A headless run records the requested mode without touching a window, so validation can exercise the
/// routing without pretending to own a display. Not a <c>Node</c>, so nothing here needs source generation.
/// </remarks>
public sealed class WindowModeSwitcher
{
    private readonly Window _window;
    private readonly WindowSize _fallback;
    private readonly WindowSize _minimum;
    private Window.ModeEnum _windowedMode = Window.ModeEnum.Windowed;
    private WindowRect? _windowed;

    /// <param name="window">The window to switch, usually the root window.</param>
    /// <param name="fallbackWindowedSize">The size to restore to when the window was never windowed this session.</param>
    /// <param name="minimumWindowedSize">The smallest size a restored window may have, screen permitting.</param>
    public WindowModeSwitcher(Window window, WindowSize fallbackWindowedSize, WindowSize minimumWindowedSize)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));
        _fallback = fallbackWindowedSize;
        _minimum = minimumWindowedSize;
        Fullscreen = IsFullscreen(window.Mode);
    }

    /// <summary>The mode last requested, which a headless run records without applying.</summary>
    public bool Fullscreen { get; private set; }

    /// <summary>Whether this process has a display whose window mode can change.</summary>
    public static bool CanSwitch => DisplayServer.GetName() != "headless";

    /// <summary>True for either fullscreen mode.</summary>
    public static bool IsFullscreen(Window.ModeEnum mode) =>
        mode is Window.ModeEnum.Fullscreen or Window.ModeEnum.ExclusiveFullscreen;

    public void Toggle() => SetFullscreen(!Fullscreen);

    /// <summary>
    /// Refits a plain window that is too large for its screen to the largest <paramref name="design"/>-shaped
    /// rectangle within <paramref name="fraction"/> of the usable area, centred
    /// (<see cref="WindowPlacement.FitIfOversized"/>). Call it once at startup, before any saved fullscreen
    /// preference is applied, so a layout designed for a screen larger than the player's opens whole and keeps
    /// its shape. A window that already fits, such as one sized by a launch argument or a test harness, is kept;
    /// so is a fullscreen or maximized window, and nothing happens headless.
    /// </summary>
    public void FitWindowed(WindowSize design, double fraction = 0.9)
    {
        if (!CanSwitch || _window.Mode != Window.ModeEnum.Windowed)
        {
            return;
        }

        Rect2I usable = DisplayServer.ScreenGetUsableRect(_window.CurrentScreen);
        WindowRect? placed = WindowPlacement.FitIfOversized(new(_window.Size.X, _window.Size.Y), design,
            new(usable.Position.X, usable.Position.Y, usable.Size.X, usable.Size.Y), fraction);
        if (placed is { } fitted)
        {
            _window.Size = new(fitted.Width, fitted.Height);
            _window.Position = new(fitted.X, fitted.Y);
        }
    }

    public void SetFullscreen(bool fullscreen)
    {
        Fullscreen = fullscreen;
        if (!CanSwitch)
        {
            return;
        }

        if (fullscreen)
        {
            if (_window.Mode is Window.ModeEnum.Windowed or Window.ModeEnum.Maximized)
            {
                _windowedMode = _window.Mode;
                if (_window.Mode == Window.ModeEnum.Windowed)
                {
                    _windowed = new(_window.Position.X, _window.Position.Y, _window.Size.X, _window.Size.Y);
                }
            }

            _window.Mode = Window.ModeEnum.Fullscreen;
            return;
        }

        if (!IsFullscreen(_window.Mode))
        {
            return;
        }

        _window.Mode = _windowedMode;
        if (_windowedMode == Window.ModeEnum.Windowed)
        {
            Rect2I usable = DisplayServer.ScreenGetUsableRect(_window.CurrentScreen);
            WindowRect placed = WindowPlacement.Restore(_windowed,
                new(usable.Position.X, usable.Position.Y, usable.Size.X, usable.Size.Y), _fallback, _minimum);
            _window.Size = new(placed.Width, placed.Height);
            _window.Position = new(placed.X, placed.Y);
        }
    }
}
