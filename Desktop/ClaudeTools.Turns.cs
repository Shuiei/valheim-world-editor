using System.Numerics;

namespace TerrainEditor.Desktop;

public sealed partial class ClaudeTools
{
    /// <summary>
    /// The point a group of objects turns round: the pivot given (each coordinate on its own), else the middle of the
    /// objects' positions.
    /// </summary>
    /// <param name="scene">The scene the objects are of.</param>
    /// <param name="ok">Their indices into the scene's things.</param>
    /// <param name="pivotX">The pivot's x, or null for the middle.</param>
    /// <param name="pivotZ">The pivot's z, or null for the middle.</param>
    internal static (float X, float Z) PivotOf(WorldScene scene, IEnumerable<int> ok, float? pivotX, float? pivotZ) =>
        (pivotX ?? ok.Average(i => scene.Things[i].Position.X), pivotZ ?? ok.Average(i => scene.Things[i].Position.Z));

    /// <summary>Euler angles each brought within 0 to 360 degrees (a turn of 383 is one of 23).</summary>
    internal static Vector3 Wrapped(Vector3 euler)
    {
        static float W(float a) => (a % 360 + 360) % 360;
        return new Vector3(W(euler.X), W(euler.Y), W(euler.Z));
    }

    /// <summary>A point turned round the vertical by degrees, as the game's yaw turns (90: north becomes east).</summary>
    internal static (float X, float Z) Turned(float x, float z, float degrees)
    {
        float a = degrees * MathF.PI / 180f, c = MathF.Cos(a), s = MathF.Sin(a);
        return (x * c + z * s, -x * s + z * c);
    }
}
