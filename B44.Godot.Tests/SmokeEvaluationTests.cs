using B44.Godot.Smoke;
using Xunit;

namespace B44.Godot.Tests;

/// <summary>
/// The pass/fail rules are deliberately free of Godot types so they can be
/// tested here, on a machine with no engine installed. The Node shell around
/// them is thin enough that the smoke workflow is its only real test.
/// </summary>
public class SmokeEvaluationTests
{
    private static SmokeObservation Ready() => new() { State = B44StartupState.Ready };

    [Fact]
    public void ReadyWithNothingMissing_Passes()
    {
        SmokeResult result = SmokeEvaluation.Evaluate(Ready());

        Assert.True(result.Passed);
        Assert.Equal(SmokeEvaluation.PassExitCode, result.ExitCode);
        Assert.Contains(SmokeEvaluation.PassMarker, result.MarkerLine, System.StringComparison.Ordinal);
    }

    [Fact]
    public void PassAndFailMarkers_AreDistinguishable()
    {
        // A crashed run prints neither. These must not be prefixes of each other,
        // or a grep for the pass token would also match a failure line.
        Assert.DoesNotContain(SmokeEvaluation.PassMarker, SmokeEvaluation.FailMarker, System.StringComparison.Ordinal);
        Assert.DoesNotContain(SmokeEvaluation.FailMarker, SmokeEvaluation.PassMarker, System.StringComparison.Ordinal);
    }

    [Fact]
    public void TimedOut_ReportsTimeout_EvenWhenOtherProblemsExist()
    {
        SmokeResult result = SmokeEvaluation.Evaluate(new SmokeObservation
        {
            State = B44StartupState.Ready,
            TimedOut = true,
            MissingAutoloads = ["SaveManager"],
        });

        // Nothing observed during a timeout is trustworthy, so it wins.
        Assert.Equal(SmokeOutcome.TimedOut, result.Outcome);
        Assert.Equal(SmokeEvaluation.FailExitCode, result.ExitCode);
    }

    [Fact]
    public void StillInitializing_IsTreatedAsTimeout()
    {
        SmokeResult result = SmokeEvaluation.Evaluate(new SmokeObservation
        {
            State = B44StartupState.Initializing,
        });

        Assert.Equal(SmokeOutcome.TimedOut, result.Outcome);
        Assert.Contains("no verdict", result.Report, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FailedStartup_SurfacesTheGameDiagnostics()
    {
        SmokeResult result = SmokeEvaluation.Evaluate(new SmokeObservation
        {
            State = B44StartupState.Failed,
            FailureDiagnostics = "TurnManager had no owner",
        });

        Assert.Equal(SmokeOutcome.StartupFailed, result.Outcome);
        Assert.Contains("TurnManager had no owner", result.Report, System.StringComparison.Ordinal);
    }

    [Fact]
    public void FailedStartupWithoutDiagnostics_SaysSoRatherThanReportingNothing()
    {
        SmokeResult result = SmokeEvaluation.Evaluate(new SmokeObservation
        {
            State = B44StartupState.Failed,
        });

        Assert.Equal(SmokeOutcome.StartupFailed, result.Outcome);
        Assert.Contains("FailureDiagnostics", result.Report, System.StringComparison.Ordinal);
    }

    [Fact]
    public void MissingAutoloads_AreNamed()
    {
        SmokeResult result = SmokeEvaluation.Evaluate(new SmokeObservation
        {
            State = B44StartupState.Ready,
            MissingAutoloads = ["SaveManager", "DataRegistry"],
        });

        Assert.Equal(SmokeOutcome.MissingAutoload, result.Outcome);
        Assert.Contains("SaveManager", result.Report, System.StringComparison.Ordinal);
        Assert.Contains("DataRegistry", result.Report, System.StringComparison.Ordinal);
    }

    [Fact]
    public void UnresolvedNodePaths_AreNamed()
    {
        SmokeResult result = SmokeEvaluation.Evaluate(new SmokeObservation
        {
            State = B44StartupState.Ready,
            UnresolvedNodePaths = ["/root/Main/Hud"],
        });

        Assert.Equal(SmokeOutcome.UnresolvedNodePath, result.Outcome);
        Assert.Contains("/root/Main/Hud", result.Report, System.StringComparison.Ordinal);
    }

    [Fact]
    public void EngineErrors_FailEvenWhenEverythingElseResolved()
    {
        SmokeResult result = SmokeEvaluation.Evaluate(new SmokeObservation
        {
            State = B44StartupState.Ready,
            EngineErrors = ["Cannot open file res://missing.tres"],
        });

        Assert.Equal(SmokeOutcome.EngineError, result.Outcome);
        Assert.Contains("res://missing.tres", result.Report, System.StringComparison.Ordinal);
    }

    [Fact]
    public void LowerPriorityProblems_AreStillReported()
    {
        // Reporting only the highest-priority failure makes a broken startup
        // take several CI runs to diagnose. One run should show everything.
        SmokeResult result = SmokeEvaluation.Evaluate(new SmokeObservation
        {
            State = B44StartupState.Ready,
            MissingAutoloads = ["SaveManager"],
            UnresolvedNodePaths = ["/root/Main/Hud"],
            EngineErrors = ["shader compile failed"],
        });

        Assert.Equal(SmokeOutcome.MissingAutoload, result.Outcome);
        Assert.Contains("SaveManager", result.Report, System.StringComparison.Ordinal);
        Assert.Contains("/root/Main/Hud", result.Report, System.StringComparison.Ordinal);
        Assert.Contains("shader compile failed", result.Report, System.StringComparison.Ordinal);
    }

    [Fact]
    public void EveryFailureOutcome_UsesTheFailExitCodeAndMarker()
    {
        SmokeObservation[] failures =
        [
            new() { TimedOut = true },
            new() { State = B44StartupState.Failed },
            new() { State = B44StartupState.Ready, MissingAutoloads = ["X"] },
            new() { State = B44StartupState.Ready, UnresolvedNodePaths = ["X"] },
            new() { State = B44StartupState.Ready, EngineErrors = ["X"] },
        ];

        foreach (SmokeObservation observation in failures)
        {
            SmokeResult result = SmokeEvaluation.Evaluate(observation);
            Assert.False(result.Passed);
            Assert.Equal(SmokeEvaluation.FailExitCode, result.ExitCode);
            Assert.StartsWith(SmokeEvaluation.FailMarker, result.MarkerLine, System.StringComparison.Ordinal);
        }
    }
}
