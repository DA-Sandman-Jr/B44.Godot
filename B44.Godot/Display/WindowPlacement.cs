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
/// Where a window goes when it leaves fullscreen, and how a windowed game fits its screen at startup.
/// Deliberately free of Godot types: <see cref="WindowModeSwitcher"/> is the thin shell that applies the answer.
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

    /// <summary>
    /// The largest window with <paramref name="design"/>'s shape that fits within <paramref name="fraction"/> of the
    /// usable area, never larger than the design itself, centred on the usable area. A portrait phone layout opened
    /// on a landscape monitor keeps its shape instead of being cut to the screen's height into a near-square window,
    /// and a design larger than the screen opens wholly visible.
    /// </summary>
    /// <param name="design">The size the game is laid out for; only its shape matters once it has to shrink.</param>
    /// <param name="usable">The usable area of the screen the window is on.</param>
    /// <param name="fraction">How much of the usable width and height the window may take, in (0, 1].</param>
    public static WindowRect Fit(WindowSize design, WindowRect usable, double fraction)
    {
        if (design.Width <= 0 || design.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(design), design, "A design size must be positive.");
        }

        if (double.IsNaN(fraction) || fraction <= 0 || fraction > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(fraction), fraction, "The screen fraction must be in (0, 1].");
        }

        int usableWidth = Math.Max(0, usable.Width);
        int usableHeight = Math.Max(0, usable.Height);
        double scale = Math.Min(1d, Math.Min(usableWidth * fraction / design.Width, usableHeight * fraction / design.Height));
        int width = Math.Clamp((int)Math.Round(design.Width * scale, MidpointRounding.AwayFromZero), 1, Math.Max(1, usableWidth));
        int height = Math.Clamp((int)Math.Round(design.Height * scale, MidpointRounding.AwayFromZero), 1, Math.Max(1, usableHeight));
        return new(usable.X + ((usableWidth - width) / 2), usable.Y + ((usableHeight - height) / 2), width, height);
    }
}
