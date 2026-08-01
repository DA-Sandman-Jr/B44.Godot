using B44.Common.Diagnostics;
using Godot;

namespace B44.Godot.Diagnostics;

/// <summary>
/// Wires the engine-free <see cref="StructuredGameLogger"/> to Godot's output
/// channels. Nothing in a game's Core calls <c>GD.*</c>; this is the bridge.
/// </summary>
/// <remarks>
/// All three games carried a byte-for-byte equivalent of this. The only
/// difference was an <c>if</c>-chain versus a <c>switch</c>, and the switch form
/// matched <see cref="LogSeverity.Error"/> exactly rather than testing
/// <c>&gt;=</c>. Those behave identically only because <c>Error</c> is currently
/// the highest severity; the threshold form kept here stays correct if a higher
/// one is ever added, which is why it is the one that survived.
/// </remarks>
public static class GodotLoggerFactory
{
    /// <summary>A logger whose sink is Godot's print, warning, and error channels.</summary>
    public static StructuredGameLogger CreateWithGodotSink() => new(WriteToGodot);

    /// <summary>
    /// Routes one formatted event to Godot. Exposed so a game that builds its own
    /// logger can still use the standard routing instead of restating it.
    /// </summary>
    public static void WriteToGodot(StructuredLogEvent logEvent, string formatted)
    {
        if (logEvent.Severity >= LogSeverity.Error)
        {
            GD.PushError(formatted);
        }
        else if (logEvent.Severity >= LogSeverity.Warning)
        {
            GD.PushWarning(formatted);
        }
        else
        {
            GD.Print(formatted);
        }
    }

    /// <summary>
    /// Warning sink for <c>RepositoryFactory.CreateWithFallback</c>, whose
    /// <c>onWarning</c> parameter is a plain <c>Action&lt;string&gt;</c>. Named
    /// here so the three games stop each writing the same lambda at their save
    /// wiring.
    /// </summary>
    public static void PushWarning(string message) => GD.PushWarning(message);
}
