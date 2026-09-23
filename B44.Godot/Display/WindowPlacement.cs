using System;

namespace B44.Godot.Display;

/// <summary>A window's position and size in screen pixels. Godot-free so placement is testable without the engine.</summary>
public readonly record struct WindowRect(int X, int Y, int Width, int Height)
{
    public int Right => X + Width;

    public int Bottom => Y + Height;
}

/// <summary>A window size in screen pixels.</summary>
public readonly record struct WindowSize(int Width, int Height);

/// <summary>
/// Where a window goes when it leaves fullscreen. Deliberately free of Godot types:
/// <see cref="WindowModeSwitcher"/> is the thin shell that applies the answer.
/// </summary>
public static class WindowPlacement
{
    /// <summary>
    /// Returns the remembered windowed bounds, or <paramref name="fallback"/> centred on the usable area when the
    /// window was never windowed this session. The result is never smaller than <paramref name="minimum"/>
    /// unless the usable area itself is, never larger than the usable area, and always wholly inside it, so a
    /// window remembered on a monitor that has since shrunk or gone comes back somewhere it can be reached.
    /// </summary>
    public static WindowRect Restore(WindowRect? remembered, WindowRect usable, WindowSize fallback, WindowSize minimum)
    {
        int usableWidth = Math.Max(0, usable.Width);
        int usableHeight = Math.Max(0, usable.Height);
        int wantedWidth = remembered?.Width ?? fallback.Width;
        int wantedHeight = remembered?.Height ?? fallback.Height;
        int width = Math.Clamp(wantedWidth, Math.Min(Math.Max(0, minimum.Width), usableWidth), usableWidth);
        int height = Math.Clamp(wantedHeight, Math.Min(Math.Max(0, minimum.Height), usableHeight), usableHeight);
        int x = remembered?.X ?? usable.X + ((usableWidth - width) / 2);
        int y = remembered?.Y ?? usable.Y + ((usableHeight - height) / 2);
        return new(
            Math.Clamp(x, usable.X, usable.X + usableWidth - width),
            Math.Clamp(y, usable.Y, usable.Y + usableHeight - height),
            width,
            height);
    }
}
