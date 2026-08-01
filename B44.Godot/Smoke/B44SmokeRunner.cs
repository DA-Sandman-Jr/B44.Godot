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
/// A game subclasses this, overrides <see cref="ProbeNodePath"/> and
/// <see cref="RequiredAutoloads"/>, and attaches the subclass to a scene whose
/// only content is that script.
///
/// **Deliberately not sealed.** Godot binds scripts to files under
/// <c>res://</c>, so a <see cref="Node"/> type living in a NuGet assembly
/// cannot be attached to a scene directly. Every consuming game declares a
/// one-line subclass in its own project purely to give the scene a script path
/// to point at. Sealing this would make the type unusable for its only purpose.
/// </summary>
public partial class B44SmokeRunner : Node
{
    // Configuration is expressed by overriding these, NOT with [Export].
    //
    // Godot's source generator does not marshal [Export] properties inherited
    // from a base class in another assembly: the generated ScriptProperties for
    // a game's subclass contains no entry for them. A scene that then sets
    // those properties fails to instantiate, silently — the scene resource
    // loads, no node is created, no _Ready runs, and the engine simply waits.
    // Verified 2026-08-01 by reading the generated sources for a real consumer.
    //
    // Plain virtual members cost a game a few lines instead of one, and they
    // work.

    /// <summary>Path to the node implementing <see cref="IB44StartupProbe"/>. Usually an autoload.</summary>
    protected virtual string ProbeNodePath => string.Empty;

    /// <summary>Autoload names that must be present, without the leading slash.</summary>
    protected virtual string[] RequiredAutoloads => [];

    /// <summary>Absolute node paths that must resolve once startup reports Ready.</summary>
    protected virtual string[] RequiredNodePaths => [];

    /// <summary>
    /// How long to wait for a verdict. The workflow also enforces a job-level
    /// timeout; this one exists so the run fails with a readable marker instead
    /// of being killed by the runner.
    /// </summary>
    protected virtual double TimeoutSeconds => 30.0;

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
                    $"No node implementing IB44StartupProbe at ProbeNodePath '{ProbeNodePath}'.",
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
        string.IsNullOrEmpty(ProbeNodePath) ? null : GetNodeOrNull(ProbeNodePath) as IB44StartupProbe;

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
