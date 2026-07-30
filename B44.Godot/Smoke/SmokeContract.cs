using System.Collections.Generic;

namespace B44.Godot.Smoke;

/// <summary>
/// Where a game is in its startup sequence. The game owns this state; the smoke
/// harness only observes it. Deliberately not the same abstraction as the
/// harness marker — the game says what happened, the harness reports it in a
/// form CI can assert on.
/// </summary>
public enum B44StartupState
{
    /// <summary>Composition is still running. Not yet a pass or a failure.</summary>
    Initializing,

    /// <summary>Every required dependency resolved; gameplay may begin.</summary>
    Ready,

    /// <summary>Startup could not complete. <see cref="IB44StartupProbe.FailureDiagnostics"/> says why.</summary>
    Failed,
}

/// <summary>
/// Implemented by a game so the shared harness can observe its startup without
/// knowing anything about its composition. This is the entire game-side surface
/// of the smoke test.
/// </summary>
public interface IB44StartupProbe
{
    B44StartupState State { get; }

    /// <summary>
    /// Actionable detail when <see cref="State"/> is
    /// <see cref="B44StartupState.Failed"/> — which dependency was missing, and
    /// where it was expected. Null otherwise.
    /// </summary>
    string? FailureDiagnostics { get; }
}

/// <summary>Why a smoke run ended. Ordered by reporting precedence, not severity.</summary>
public enum SmokeOutcome
{
    Passed,
    TimedOut,
    StartupFailed,
    MissingAutoload,
    UnresolvedNodePath,
    EngineError,
}

/// <summary>
/// Everything the runner saw, gathered from the engine and handed to the pure
/// evaluator. Exists so the pass/fail decision can be tested without Godot.
/// </summary>
public sealed record SmokeObservation
{
    public B44StartupState State { get; init; } = B44StartupState.Initializing;

    public string? FailureDiagnostics { get; init; }

    /// <summary>Autoload names that were required but absent from the scene tree.</summary>
    public IReadOnlyCollection<string> MissingAutoloads { get; init; } = [];

    /// <summary>Declared node paths that did not resolve.</summary>
    public IReadOnlyCollection<string> UnresolvedNodePaths { get; init; } = [];

    /// <summary>Engine errors and unhandled exceptions captured during startup.</summary>
    public IReadOnlyCollection<string> EngineErrors { get; init; } = [];

    /// <summary>True when the wait budget elapsed before the game reported Ready or Failed.</summary>
    public bool TimedOut { get; init; }
}

/// <summary>
/// The harness verdict. <see cref="MarkerLine"/> is the single line CI asserts
/// on; <see cref="Report"/> is for a human reading the job log.
/// </summary>
public sealed record SmokeResult(SmokeOutcome Outcome, string MarkerLine, int ExitCode, string Report)
{
    public bool Passed => Outcome == SmokeOutcome.Passed;
}
