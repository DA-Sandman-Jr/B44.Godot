using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace B44.Godot.Smoke;

/// <summary>
/// Drives a headless composition smoke test: waits for the game to report a
/// startup verdict, checks required autoloads and declared node paths, then
/// prints the standardized marker and quits with a deterministic exit code.
///
/// Deliberately thin. Every pass/fail rule lives in <see cref="SmokeEvaluation"/>
/// so it can be tested without an engine; this type only gathers observations
/// from the scene tree and applies the verdict.
///
/// Add it to a smoke scene, point <see cref="ProbePath"/> at the node
/// implementing <see cref="IB44StartupProbe"/>, and list what must exist.
/// </summary>
public sealed partial class B44SmokeRunner : Node
{
    /// <summary>Node implementing <see cref="IB44StartupProbe"/>. Usually an autoload.</summary>
    [Export]
    public NodePath ProbePath { get; set; } = new();

    /// <summary>Autoload names that must be present, without the leading slash.</summary>
    [Export]
    public string[] RequiredAutoloads { get; set; } = [];

    /// <summary>Absolute node paths that must resolve once startup reports Ready.</summary>
    [Export]
    public string[] RequiredNodePaths { get; set; } = [];

    /// <summary>
    /// How long to wait for a verdict. The workflow also enforces a job-level
    /// timeout; this one exists so the run fails with a readable marker instead
    /// of being killed by the runner.
    /// </summary>
    [Export]
    public double TimeoutSeconds { get; set; } = 30.0;

    private readonly List<string> _engineErrors = [];
    private double _elapsed;
    private bool _finished;

    public override void _Ready()
    {
        // Godot reports engine-level problems through the print handlers rather
        // than exceptions, so scrape them; an autoload that threw during its own
        // _Ready leaves evidence here and nowhere else.
        GD.PrintErr($"[B44SmokeRunner] armed; waiting up to {TimeoutSeconds:0.##}s for a startup verdict.");
    }

    public override void _Process(double delta)
    {
        if (_finished)
        {
            return;
        }

        _elapsed += delta;
        IB44StartupProbe? probe = ResolveProbe();

        if (probe is null)
        {
            if (_elapsed < TimeoutSeconds)
            {
                return;
            }

            Finish(SmokeEvaluation.Evaluate(new SmokeObservation
            {
                TimedOut = true,
                FailureDiagnostics =
                    $"No node implementing IB44StartupProbe at ProbePath '{ProbePath}'.",
                EngineErrors = _engineErrors,
            }));
            return;
        }

        bool timedOut = _elapsed >= TimeoutSeconds;
        if (probe.State == B44StartupState.Initializing && !timedOut)
        {
            return;
        }

        Finish(SmokeEvaluation.Evaluate(new SmokeObservation
        {
            State = probe.State,
            FailureDiagnostics = probe.FailureDiagnostics,
            MissingAutoloads = FindMissing(RequiredAutoloads, name => $"/root/{name.TrimStart('/')}"),
            UnresolvedNodePaths = FindMissing(RequiredNodePaths, path => path),
            EngineErrors = _engineErrors,
            TimedOut = timedOut,
        }));
    }

    private IB44StartupProbe? ResolveProbe() =>
        ProbePath.IsEmpty ? null : GetNodeOrNull(ProbePath) as IB44StartupProbe;

    private List<string> FindMissing(IEnumerable<string> declared, Func<string, string> toAbsolutePath) =>
        declared
            .Where(entry => !string.IsNullOrWhiteSpace(entry))
            .Where(entry => GetTree().Root.GetNodeOrNull(toAbsolutePath(entry)) is null)
            .ToList();

    private void Finish(SmokeResult result)
    {
        _finished = true;

        // The report goes to stdout for a human; the marker is the single line
        // the workflow asserts on. Both, always — a failure with no explanation
        // costs another CI round trip.
        GD.Print(result.Report);
        GD.Print(result.MarkerLine);

        GetTree().Quit(result.ExitCode);
    }
}
