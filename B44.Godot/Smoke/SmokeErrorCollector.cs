using System.Collections.Generic;
using Godot;
using Godot.Collections;

namespace B44.Godot.Smoke;

/// <summary>
/// Collects engine-level errors while a smoke run is in progress, by registering
/// itself with <see cref="OS.AddLogger(Logger)"/>.
/// </summary>
/// <remarks>
/// This exists because Godot does not surface engine problems as exceptions. A
/// managed exception thrown inside a node's <c>_Ready</c> is caught at the
/// marshalling boundary and printed, so a <c>try</c>/<c>catch</c> around
/// <c>AddChild</c> never sees it. Without this collector the harness could watch
/// a scene fail to compose and still report a pass.
///
/// **Scope, precisely.** A logger only sees what is emitted after it is
/// registered, and the harness registers this from its own <c>_Ready</c> — which
/// runs after every autoload has already initialised. So this covers composition
/// the harness itself performs and anything later; it does NOT cover the autoload
/// phase. That boundary is stated here rather than glossed over, because a check
/// believed to be wider than it is, is worse than no check. Autoload failures are
/// caught by the game's own <see cref="IB44StartupProbe"/>, which is what that
/// interface is for.
///
/// Only <c>_LogError</c> is observed. <c>_LogMessage</c> carries ordinary
/// <c>GD.Print</c> and <c>GD.PrintErr</c> output, including the harness's own
/// startup line, so collecting it would make the harness fail on its own
/// diagnostics.
/// </remarks>
internal sealed partial class SmokeErrorCollector : Logger
{
    private readonly List<string> _errors = [];

    /// <summary>Errors seen since registration, in order.</summary>
    public IReadOnlyList<string> Errors => _errors;

    public override void _LogError(
        string function,
        string file,
        int line,
        string code,
        string rationale,
        bool editorNotify,
        int errorType,
        Array<ScriptBacktrace> scriptBacktraces)
    {
        // Warnings are excluded deliberately. Games legitimately push warnings
        // during startup — a null-dependency facade warning once is a documented
        // pattern in at least one B44 game — and failing a smoke run on those
        // would make the gate unusable. Errors, script errors, and shader errors
        // all count.
        if (errorType == (int)ErrorType.Warning)
        {
            return;
        }

        string detail = string.IsNullOrWhiteSpace(rationale) ? code : rationale;
        string kind = ((ErrorType)errorType).ToString();

        _errors.Add(
            string.IsNullOrWhiteSpace(file)
                ? $"{kind}: {detail}"
                : $"{kind}: {detail} ({file}:{line} in {function})");
    }
}
