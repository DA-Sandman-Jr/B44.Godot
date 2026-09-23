using System;
using B44.Godot.Display;
using Xunit;

namespace B44.Godot.Tests;

/// <summary>
/// Where a window returns from fullscreen is decided without Godot types, so it is tested here; the
/// switcher that applies it is a thin shell over the window.
/// </summary>
public class WindowPlacementTests
{
    private static readonly WindowRect Screen = new(0, 0, 1920, 1040);
    private static readonly WindowSize Fallback = new(1280, 800);
    private static readonly WindowSize Minimum = new(800, 600);

    [Fact]
    public void RememberedBoundsThatFitAreRestoredExactly()
    {
        var remembered = new WindowRect(100, 60, 1024, 700);

        Assert.Equal(remembered, WindowPlacement.Restore(remembered, Screen, Fallback, Minimum));
    }

    [Fact]
    public void AWindowNeverWindowedThisSessionIsCentredAtTheFallbackSize()
    {
        Assert.Equal(new WindowRect(320, 120, 1280, 800), WindowPlacement.Restore(null, Screen, Fallback, Minimum));
    }

    [Fact]
    public void AWindowLargerThanTheScreenShrinksToFitIt()
    {
        var remembered = new WindowRect(-50, -20, 2560, 1440);

        Assert.Equal(Screen, WindowPlacement.Restore(remembered, Screen, Fallback, Minimum));
    }

    [Fact]
    public void ATinyRememberedWindowGrowsToTheMinimum()
    {
        var remembered = new WindowRect(10, 10, 200, 150);

        Assert.Equal(new WindowRect(10, 10, 800, 600), WindowPlacement.Restore(remembered, Screen, Fallback, Minimum));
    }

    [Fact]
    public void AWindowLeftOnAMonitorThatIsGoneComesBackOnscreen()
    {
        var remembered = new WindowRect(3000, 1200, 1024, 700);

        Assert.Equal(new WindowRect(896, 340, 1024, 700), WindowPlacement.Restore(remembered, Screen, Fallback, Minimum));
    }

    [Fact]
    public void AScreenSmallerThanTheMinimumWinsOverTheMinimum()
    {
        var small = new WindowRect(0, 0, 640, 480);

        Assert.Equal(small, WindowPlacement.Restore(null, small, Fallback, Minimum));
    }

    [Fact]
    public void AnOffsetUsableAreaIsRespected()
    {
        var secondScreen = new WindowRect(1920, 40, 1280, 984);

        WindowRect placed = WindowPlacement.Restore(null, secondScreen, Fallback, Minimum);

        Assert.Equal(new WindowRect(1920, 132, 1280, 800), placed);
        Assert.True(placed.X >= secondScreen.X && placed.Right <= secondScreen.Right);
        Assert.True(placed.Y >= secondScreen.Y && placed.Bottom <= secondScreen.Bottom);
    }

    [Fact]
    public void AnEmptyUsableAreaYieldsAnEmptyWindowRatherThanThrowing()
    {
        var none = new WindowRect(0, 0, 0, 0);

        Assert.Equal(none, WindowPlacement.Restore(null, none, Fallback, Minimum));
    }

    [Fact]
    public void APortraitDesignTallerThanTheScreenKeepsItsShapeAndFits()
    {
        // A 1080x1920 phone layout on a 1080p desktop: cut to the screen height it would be near-square.
        WindowRect placed = WindowPlacement.Fit(new WindowSize(1080, 1920), Screen, 0.9);

        Assert.Equal(new WindowRect(696, 52, 527, 936), placed);
        Assert.Equal(1080d / 1920d, (double)placed.Width / placed.Height, 2);
    }

    [Fact]
    public void ADesignThatAlreadyFitsIsCentredAtItsOwnSize()
    {
        Assert.Equal(new WindowRect(760, 220, 400, 600), WindowPlacement.Fit(new WindowSize(400, 600), Screen, 0.9));
    }

    [Fact]
    public void ALandscapeDesignOnAPortraitScreenFitsItsWidth()
    {
        var portraitScreen = new WindowRect(0, 0, 1080, 1920);

        Assert.Equal(new WindowRect(0, 656, 1080, 608), WindowPlacement.Fit(new WindowSize(1600, 900), portraitScreen, 1));
    }

    [Fact]
    public void AFittedWindowStaysOnAnOffsetScreen()
    {
        var secondScreen = new WindowRect(1920, 40, 1280, 984);

        WindowRect placed = WindowPlacement.Fit(new WindowSize(1080, 1920), secondScreen, 0.9);

        Assert.Equal(new WindowRect(2311, 89, 498, 886), placed);
        Assert.True(placed.X >= secondScreen.X && placed.Right <= secondScreen.Right);
        Assert.True(placed.Y >= secondScreen.Y && placed.Bottom <= secondScreen.Bottom);
    }

    [Fact]
    public void AnEmptyUsableAreaStillYieldsAWindow()
    {
        Assert.Equal(new WindowRect(0, 0, 1, 1), WindowPlacement.Fit(new WindowSize(1080, 1920), new WindowRect(0, 0, 0, 0), 0.9));
    }

    [Theory]
    [InlineData(0d)]
    [InlineData(-0.5d)]
    [InlineData(1.5d)]
    [InlineData(double.NaN)]
    public void AScreenFractionOutsideTheUnitIntervalIsRefused(double fraction)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => WindowPlacement.Fit(new WindowSize(1080, 1920), Screen, fraction));
    }

    [Fact]
    public void AWindowThatAlreadyFitsIsKept()
    {
        // A launch argument or harness asked for 1280x720; it fits a 1080p screen, so it is not second-guessed.
        Assert.Null(WindowPlacement.FitIfOversized(new WindowSize(1280, 720), new WindowSize(1080, 1920), Screen, 0.9));
    }

    [Fact]
    public void AWindowTooTallForItsScreenIsRefitted()
    {
        Assert.Equal(new WindowRect(696, 52, 527, 936),
            WindowPlacement.FitIfOversized(new WindowSize(1080, 1920), new WindowSize(1080, 1920), Screen, 0.9));
    }

    [Fact]
    public void AWindowTooWideForItsScreenIsRefitted()
    {
        Assert.NotNull(WindowPlacement.FitIfOversized(new WindowSize(1800, 600), new WindowSize(1080, 1920), Screen, 0.9));
    }

    [Fact]
    public void AnEmptyDesignIsRefused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => WindowPlacement.Fit(new WindowSize(0, 1920), Screen, 0.9));
    }
}
