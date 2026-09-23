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
}
