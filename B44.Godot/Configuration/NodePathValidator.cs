using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;

namespace B44.Godot.Configuration;

/// <summary>
/// Validates that the nodes a scene's <c>*Paths</c> resource declares actually
/// exist on the node that owns it.
/// </summary>
/// <remarks>
/// Taken from TicTacHoe's copy, which was identical to Whispers' except that it
/// raises a descriptive <see cref="InvalidOperationException"/> where Whispers
/// raised a bare <see cref="ArgumentNullException"/> naming only the property.
///
/// **A missing node is reported with <see cref="GD.PushError(string)"/>, not
/// <c>GD.PrintErr</c>, and that is a deliberate change from both copies.**
/// <c>GD.PrintErr</c> writes a message; only <c>PushError</c> reaches Godot's
/// error channel, which is what <c>B44SmokeRunner</c> observes. With the old
/// call a renamed or moved node printed a line nobody read and the smoke test
/// still passed. Validation that cannot fail a build is decoration.
///
/// It reports rather than throws because Godot catches exceptions thrown inside
/// <c>_Ready</c> at the marshalling boundary and prints them, so throwing here
/// would not propagate to a caller anyway — it would just be a noisier way of
/// reaching the same channel.
/// </remarks>
public static class NodePathValidator
{
    /// <summary>Reports an error when <paramref name="path"/> does not resolve from <paramref name="owner"/>.</summary>
    public static void AssertHasNode(Node owner, NodePath path, string propertyName)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(path);

        if (!owner.HasNode(path))
        {
            GD.PushError($"{owner.Name} is missing required node for {propertyName}: {path}");
        }
    }

    /// <summary>
    /// Validates every <c>[Export] NodePath</c> property on a paths resource
    /// against the node that owns it, reporting each miss as an error.
    /// </summary>
    public static void ValidateExportedNodePaths(Node owner, object pathConfiguration)
    {
        foreach (string missing in FindMissingNodePaths(owner, pathConfiguration))
        {
            GD.PushError($"{owner.Name} is missing required node: {missing}");
        }
    }

    /// <summary>
    /// Returns the declared paths that do not resolve, as
    /// <c>PropertyName at 'path'</c>, instead of reporting them.
    /// </summary>
    /// <remarks>
    /// Exists so one description of "what this paths resource requires" serves
    /// both uses: a game validates at runtime and gets errors, while the smoke
    /// harness collects the same misses and reports them as
    /// <c>UnresolvedNodePath</c> — the outcome that names the actual problem,
    /// rather than the generic engine-error bucket a pushed error would land in.
    /// </remarks>
    public static IReadOnlyList<string> FindMissingNodePaths(Node owner, object pathConfiguration)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(pathConfiguration);

        IEnumerable<PropertyInfo> exportedNodePathProperties = pathConfiguration
            .GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.PropertyType == typeof(NodePath))
            .Where(property => property.GetCustomAttribute<ExportAttribute>() != null);

        var missing = new List<string>();

        foreach (PropertyInfo property in exportedNodePathProperties)
        {
            if (property.GetValue(pathConfiguration) is not NodePath path)
            {
                throw new InvalidOperationException(
                    $"{pathConfiguration.GetType().Name}.{property.Name} must be a non-null NodePath.");
            }

            if (!owner.HasNode(path))
            {
                missing.Add($"{property.Name} at '{path}'");
            }
        }

        return missing;
    }
}
