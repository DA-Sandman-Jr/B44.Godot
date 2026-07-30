using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace B44.Godot.Smoke;

/// <summary>
/// Turns a <see cref="SmokeObservation"/> into a verdict. Deliberately free of
/// Godot types so the pass/fail rules are unit-testable on a machine with no
/// engine installed — which is most machines, including CI runners before the
/// Godot install step.
///
/// This type owns the marker and exit-code contract. Games conform to it; they
/// do not invent their own, because three games inventing three protocols is
/// the duplication this package exists to prevent.
/// </summary>
public static class SmokeEvaluation
{
    /// <summary>Printed on success. The workflow asserts this exact token appears.</summary>
    public const string PassMarker = "B44_SMOKE_PASS";

    /// <summary>Printed on failure. Present so a crashed run and a failed run look different.</summary>
    public const string FailMarker = "B44_SMOKE_FAIL";

    public const int PassExitCode = 0;
    public const int FailExitCode = 1;

    /// <summary>
    /// Evaluates a completed observation. Failure precedence is fixed so the
    /// same broken startup always reports the same outcome: a timeout hides
    /// everything (nothing else is trustworthy), then the game's own failure
    /// verdict, then missing autoloads, then unresolved paths, then engine
    /// errors. A run that never reached <see cref="B44StartupState.Ready"/>
    /// without timing out is treated as a timeout, since the harness was asked
    /// for a verdict before one existed.
    /// </summary>
    public static SmokeResult Evaluate(SmokeObservation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);

        if (observation.TimedOut || observation.State == B44StartupState.Initializing)
        {
            return Fail(
                SmokeOutcome.TimedOut,
                observation,
                observation.TimedOut
                    ? "Startup did not reach Ready or Failed within the wait budget."
                    : "Evaluated while startup was still Initializing; no verdict was available.");
        }

        if (observation.State == B44StartupState.Failed)
        {
            return Fail(
                SmokeOutcome.StartupFailed,
                observation,
                "The game reported a failed startup: " +
                (string.IsNullOrWhiteSpace(observation.FailureDiagnostics)
                    ? "no diagnostics supplied (the game should populate FailureDiagnostics)."
                    : observation.FailureDiagnostics));
        }

        if (observation.MissingAutoloads.Count > 0)
        {
            return Fail(
                SmokeOutcome.MissingAutoload,
                observation,
                "Required autoloads absent from the scene tree: " +
                string.Join(", ", observation.MissingAutoloads));
        }

        if (observation.UnresolvedNodePaths.Count > 0)
        {
            return Fail(
                SmokeOutcome.UnresolvedNodePath,
                observation,
                "Declared node paths did not resolve: " +
                string.Join(", ", observation.UnresolvedNodePaths));
        }

        if (observation.EngineErrors.Count > 0)
        {
            return Fail(
                SmokeOutcome.EngineError,
                observation,
                "Engine errors during startup:" + Environment.NewLine +
                string.Join(Environment.NewLine, observation.EngineErrors.Select(e => "  " + e)));
        }

        return new SmokeResult(
            SmokeOutcome.Passed,
            $"{PassMarker} outcome={SmokeOutcome.Passed}",
            PassExitCode,
            "Startup reached Ready with all required autoloads and declared paths resolved.");
    }

    private static SmokeResult Fail(SmokeOutcome outcome, SmokeObservation observation, string summary)
    {
        var report = new StringBuilder();
        report.Append(summary);

        // Always append what else was wrong. Reporting only the highest-priority
        // failure makes a broken startup take several CI runs to diagnose.
        AppendIfAny(report, "Also missing autoloads", observation.MissingAutoloads, outcome, SmokeOutcome.MissingAutoload);
        AppendIfAny(report, "Also unresolved node paths", observation.UnresolvedNodePaths, outcome, SmokeOutcome.UnresolvedNodePath);
        AppendIfAny(report, "Also engine errors", observation.EngineErrors, outcome, SmokeOutcome.EngineError);

        if (outcome != SmokeOutcome.StartupFailed && !string.IsNullOrWhiteSpace(observation.FailureDiagnostics))
        {
            report.Append(Environment.NewLine).Append("Game diagnostics: ").Append(observation.FailureDiagnostics);
        }

        return new SmokeResult(
            outcome,
            $"{FailMarker} outcome={outcome}",
            FailExitCode,
            report.ToString());
    }

    private static void AppendIfAny(
        StringBuilder report,
        string label,
        IReadOnlyCollection<string> values,
        SmokeOutcome outcome,
        SmokeOutcome alreadyReported)
    {
        if (outcome == alreadyReported || values.Count == 0)
        {
            return;
        }

        report.Append(Environment.NewLine).Append(label).Append(": ").Append(string.Join(", ", values));
    }
}
