using System.Numerics;
using TerrainEditor.Terrain;

namespace WorldEditor.Tests;

/// <summary>
/// The terrain modifier tests' helper: a modifier with only the settings the test names, the rest left off.
/// </summary>
public static class TerrainModifierSupport
{
    /// <summary>
    /// A modifier at a world position. Levelling, smoothing and painting are off unless given a radius; the sort order,
    /// the player flag and the creation time are as named.
    /// </summary>
    public static TerrainModifiers.Modifier Make(Vector3 at, bool level = false, float levelRadius = 0, bool smooth = false, float smoothRadius = 0, float smoothPower = 1,
        bool paintCleared = false, float paintRadius = 0, TerrainModifiers.Paint paint = TerrainModifiers.Paint.Dirt, float paintStrength = 1, bool paintHeightCheck = false,
        int sortOrder = 0, bool player = false, long time = 0) =>
        new(at, level, 0f, levelRadius, false, smooth, smoothRadius, smoothPower, paintCleared, paintRadius, paint, paintStrength, paintHeightCheck, sortOrder, player, time);
}
