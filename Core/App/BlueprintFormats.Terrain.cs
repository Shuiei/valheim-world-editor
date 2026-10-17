namespace TerrainEditor.App;

/// <summary>
/// The terrain entries of a blueprint: how far each reaches, whether it covers a ground point, and its paint.
/// </summary>
public static partial class BlueprintFormats
{
    /// <summary>
    /// How far a terrain entry reaches from its middle along x and z: its radius, or for a square turned by its
    /// rotation, half its side times (|cos| + |sin|) of the turn (a corner, at 45° the half-diagonal).
    /// </summary>
    private static float Reach(TerrainMod t)
    {
        if (t.Shape != "square")
        {
            return t.Radius;
        }
        float a = t.Rotation * MathF.PI / 180f;
        return t.Radius * (MathF.Abs(MathF.Cos(a)) + MathF.Abs(MathF.Sin(a)));
    }

    /// <summary>
    /// Whether a terrain entry covers the ground point (x, z): a circle, or a square turned by its rotation.
    /// </summary>
    private static bool Covers(TerrainMod t, float x, float z)
    {
        float dx = x - t.Position.X, dz = z - t.Position.Z;
        if (t.Shape != "square")
        {
            return dx * dx + dz * dz <= t.Radius * t.Radius;
        }
        // A square turned by the entry's rotation (Unity yaw: clockwise seen from above).
        float a = -t.Rotation * MathF.PI / 180f, c = MathF.Cos(a), s = MathF.Sin(a);
        float lx = dx * c + dz * s, lz = -dx * s + dz * c;
        return MathF.Abs(lx) <= t.Radius && MathF.Abs(lz) <= t.Radius;
    }

    /// <summary>
    /// PlanBuild's paint names -&gt; the game's paint mask (r dirt, g cultivated, b paved, a vegetation).
    /// </summary>
    private static float[]? PaintOf(string paint) => paint.ToLowerInvariant() switch
    {
        var p when p.Contains("dirt") => [1f, 0f, 0f, 1f],
        var p when p.Contains("cultiv") => [0f, 1f, 0f, 1f],
        var p when p.Contains("paved") => [0f, 0f, 1f, 1f],
        _ => null,
    };
}
