using System.Numerics;
using TerrainEditor.Save;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The saved selections as the tests build them: the kinds Beech1 and a wall, a thing of a kind at a point, a selection
/// and its items.
/// </summary>
internal static class SavedSelectionsData
{
    internal static readonly int Beech = StableHash.Of("Beech1"), Wall = StableHash.Of("wood_wall_half");

    /// <summary>
    /// A thing of this kind at world point (x, y, z), gone or not.
    /// </summary>
    internal static WorldScene.Thing T(int id, int prefab, float x, float y, float z, bool gone = false) => new(id, prefab, new Vector3(x, y, z), Vector3.Zero, 1, false) { Gone = gone };

    /// <summary>
    /// A saved selection named "s" holding these items.
    /// </summary>
    internal static SavedSelections.Saved S(params SavedSelections.Item[] items) => new("s", [.. items]);

    /// <summary>
    /// A saved item of this kind at (x, y, z).
    /// </summary>
    internal static SavedSelections.Item I(int prefab, float x, float y, float z) => new(prefab, x, y, z);
}
