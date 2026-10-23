using System.Numerics;
using ValheimWorldEditor.Sdk;

namespace ValheimRAFT.WorldEditor;

/// <summary>
/// The rope ladders (<see cref="Keys.RopeLadder"/>): the mod hangs their rope and rungs while the game runs, from the
/// ladder down to what is below it, so the game's files have only the top. Drawn as the mod lays them out: two ropes
/// 0.8 m apart down the ladder's frame and a rung every half metre, as deep as the ground below its tie point, or 2 m
/// into the water where the ground is deeper (40 m at most). The mod stops at anything below it, pieces and rocks too:
/// the editor knows only the ground and the water.
/// </summary>
internal sealed class RopeLadders : IShapeProvider
{
    /// <summary>Where the ladder looks down from, in its frame: its "attachpoint".</summary>
    public static readonly Vector3 Tie = new(0, 0, -0.5f);

    /// <summary>The game's water level, and how far into the water the mod lets a ladder hang.</summary>
    private const float Water = 30, IntoWater = 2;

    /// <summary>The ladder's longest, the space between rungs, its ropes' half spacing and thickness.</summary>
    private const float Longest = 40, Step = 0.5f, Side = 0.4f, Half = 0.03f;

    /// <summary>Hemp rope, and the rungs' wood.</summary>
    private static readonly Vector4 Hemp = new(0.45f, 0.36f, 0.24f, 1), Wood = new(0.42f, 0.3f, 0.18f, 1);

    /// <inheritdoc/>
    public IEnumerable<Shape> ShapesOf(IObjectView obj, IWorldView world)
    {
        float length = Length(obj, world);
        // Rounded as the game rounds (half to even).
        int rungs = (int)Math.Round(length / Step);
        if (rungs == 0)
        {
            yield break;
        }
        float bottom = -Step * rungs;
        foreach (float x in new[] { Side, -Side })
        {
            yield return Bar(new Vector3(x, 0, 0), new Vector3(x, bottom, 0), Hemp);
        }
        for (int i = 1; i <= rungs; i++)
        {
            yield return Bar(new Vector3(-Side, -Step * i, 0), new Vector3(Side, -Step * i, 0), Wood);
        }
    }

    /// <summary>
    /// How far the mod hangs a ladder: from the ladder's height down to the ground below its tie point, or to 2 m into
    /// the water where the ground is deeper; down to 2 m into the water where the editor does not have the ground.
    /// </summary>
    public static float Length(IObjectView ladder, IWorldView world)
    {
        var tie = ladder.Position + Vector3.Transform(Tie * ladder.Scale, ladder.Orientation);
        float top = ladder.Position.Y;
        float water = top - Water + IntoWater;
        float length = world is IGroundView ground && ground.TryGroundHeight(tie.X, tie.Z, out float floor)
            ? floor < Water ? Math.Min(top - floor, water) : top - floor
            : water;
        return Math.Max(0, Math.Min(length, Longest));
    }

    /// <summary>A thin bar from one point to another, in the ladder's frame: two crossed ribbons, seen from both sides.</summary>
    private static Shape Bar(Vector3 from, Vector3 to, Vector4 colour)
    {
        var dir = Vector3.Normalize(to - from);
        var side = Vector3.Cross(dir, Vector3.UnitZ);
        side = side.LengthSquared() < 1e-6f ? Vector3.UnitX : Vector3.Normalize(side);
        var up = Vector3.Normalize(Vector3.Cross(side, dir));
        side *= Half;
        up *= Half;
        Vector3[] corners = [from - side, from + side, to + side, to - side, from - up, from + up, to + up, to - up];
        return new Shape(corners, [0, 1, 2, 0, 2, 3, 4, 5, 6, 4, 6, 7], colour) { BothSides = true };
    }
}
