using System.Numerics;

namespace Tunnelheim.WorldEditor;

/// <summary>
/// The edits the Tunnel tool makes, as Tunnelheim makes them in the game for the mod's own actions (pickaxe, Add Earth,
/// the smoothing pieces), and as runs of the same edits for the editor's shapes (bore, room, shaft, stairs): whatever
/// the tool does, the game replays it as its own.
/// </summary>
public static class DigOps
{
    private const float RadToDeg = 57.29578f;

    /// <summary>How far an Add Earth lump is set out from the rock it is aimed at, and its radius.</summary>
    private const float FillOffset = 0.4f, FillRadius = 1f;

    /// <summary>An aimed edit, its direction kept as the mod writes it (pitch in thousandths of a degree, yaw in degrees).</summary>
    /// <param name="kind">The edit.</param>
    /// <param name="at">Its point.</param>
    /// <param name="value">Its size.</param>
    /// <param name="dir">Its direction (any length but zero).</param>
    public static DigOp Aimed(DigKind kind, Vector3 at, float value, Vector3 dir)
    {
        var d = RockMath.Normalize(dir);
        float yaw = (float)((float)Math.Atan2(d.X, d.Z) * (double)RadToDeg);
        float pitch = (float)((float)Math.Asin(RockMath.Clamp(d.Y, -1f, 1f)) * (double)RadToDeg);
        return new DigOp(kind, at, value, RockMath.Round((float)(pitch * 1000.0)), yaw);
    }

    /// <summary>A round pickaxe dig (VoxelWorld.Dig): a chip at the point, along the rock's normal there.</summary>
    /// <param name="grid">The rock, for its normal.</param>
    /// <param name="at">Where the rock or ground was hit.</param>
    /// <param name="swing">The swing's direction.</param>
    /// <param name="size">The dig's size (the pickaxe's: 1 antler to 1.75 black metal, 2.5 the tunneller).</param>
    public static DigOp RoundDig(RockGrid grid, Vector3 at, Vector3 swing, float size)
    {
        var dir = Swing(swing);
        return Aimed(DigKind.Chip, at, size, grid.SurfaceNormal(at, -dir));
    }

    /// <summary>
    /// A square pickaxe dig (the mod's square mode): a box twice the dig's size, snapped to a grid of its own size, a
    /// little into the rock from the point.
    /// </summary>
    /// <param name="at">Where the rock or ground was hit.</param>
    /// <param name="swing">The swing's direction.</param>
    /// <param name="size">The dig's size.</param>
    public static DigOp SquareDig(Vector3 at, Vector3 swing, float size)
    {
        var q = at + (Swing(swing) * 0.35f);
        float s = 2f * size;
        float Snap(float v) => (float)((Math.Floor(v / s) + 0.5) * s);
        return new DigOp(DigKind.Box, new Vector3(Snap(q.X), Snap(q.Y), Snap(q.Z)), size + 0.02f);
    }

    /// <summary>Add Earth: a lump of rock a metre round, set out from the surface along its normal.</summary>
    /// <param name="at">The aimed point.</param>
    /// <param name="normal">The surface's normal there.</param>
    public static DigOp AddEarth(Vector3 at, Vector3 normal) => new(DigKind.Fill, at + (RockMath.Normalize(normal) * FillOffset), FillRadius);

    /// <summary>
    /// Whether Add Earth may go there: not higher than the building height above the ground the column was first dug
    /// from (the lump's lowest point at most that high).
    /// </summary>
    /// <param name="grid">The rock.</param>
    /// <param name="fill">The Add Earth edit.</param>
    public static bool MayFill(RockGrid grid, in DigOp fill) =>
        fill.Position.Y - fill.Value <= grid.SeedAt(RockMath.Round(fill.Position.X / RockGrid.Step), RockMath.Round(fill.Position.Z / RockGrid.Step)) + grid.MaxBuildHeight;

    /// <summary>The smoothing pieces (Smooth Surface, Level Wall, Level Ceiling): rock eased towards a plane.</summary>
    /// <param name="at">The aimed point.</param>
    /// <param name="normal">The plane's normal, solid behind it.</param>
    /// <param name="radius">How far it reaches.</param>
    public static DigOp Plane(Vector3 at, Vector3 normal, float radius) => Aimed(DigKind.Plane, at, radius, normal);

    /// <summary>Level Wall's normal: against the horizontal look direction (the hit's own when looking straight down).</summary>
    /// <param name="look">The look direction.</param>
    /// <param name="hitNormal">The hit surface's normal.</param>
    public static Vector3 WallNormal(Vector3 look, Vector3 hitNormal)
    {
        var flat = new Vector3(-look.X, 0, -look.Z);
        if (RockMath.LengthSquared(flat) > 1e-4f)
        {
            return RockMath.Normalize(flat);
        }
        flat = new Vector3(hitNormal.X, 0, hitNormal.Z);
        return RockMath.LengthSquared(flat) > 1e-4f ? RockMath.Normalize(flat) : Vector3.UnitZ;
    }

    /// <summary>Level Ceiling's normal: straight down.</summary>
    public static Vector3 CeilingNormal { get; } = -Vector3.UnitY;

    /// <summary>
    /// A bore: a round tunnel from one point to another, carved as spheres of its radius a quarter of their width
    /// apart (at most 400).
    /// </summary>
    /// <param name="from">Its start (the middle of its mouth).</param>
    /// <param name="to">Its end.</param>
    /// <param name="radius">Its radius.</param>
    public static List<DigOp> Bore(Vector3 from, Vector3 to, float radius)
    {
        float length = Vector3.Distance(from, to);
        int steps = Math.Min(400, Math.Max(1, (int)Math.Ceiling(length / (radius * 0.5f))));
        var ops = new List<DigOp>(steps + 1);
        for (int i = 0; i <= steps; i++)
        {
            ops.Add(new DigOp(DigKind.Sphere, Vector3.Lerp(from, to, i / (float)steps), radius));
        }
        return ops;
    }

    /// <summary>
    /// A room: a box of rock taken out, its floor at the given height, filled with cubes a metre either way of their
    /// middle (the mod's boxes): each size rounded up to whole cubes (2 m), centred on the middle across, from the floor
    /// up.
    /// </summary>
    /// <param name="floor">The middle of its floor.</param>
    /// <param name="width">Its size east to west, in metres.</param>
    /// <param name="depth">Its size north to south.</param>
    /// <param name="height">Its height.</param>
    public static List<DigOp> Room(Vector3 floor, float width, float depth, float height)
    {
        var ops = new List<DigOp>();
        foreach (float y in Centres(floor.Y, floor.Y + height))
        {
            foreach (float z in Centred(floor.Z, depth))
            {
                foreach (float x in Centred(floor.X, width))
                {
                    ops.Add(new DigOp(DigKind.Box, new Vector3(x, y, z), 1f));
                }
            }
        }
        return ops;
    }

    /// <summary>A shaft: a square hole straight down from a point, as wide as twice its size, its boxes from its depth up to the point.</summary>
    /// <param name="top">The middle of its mouth.</param>
    /// <param name="size">Half its width.</param>
    /// <param name="depth">How deep it goes.</param>
    public static List<DigOp> Shaft(Vector3 top, float size, float depth) =>
        [.. Centres(top.Y - depth, top.Y, size * 2).Select(y => new DigOp(DigKind.Box, new Vector3(top.X, y, top.Z), size))];

    /// <summary>Stairs going down from a point in a direction (the mod's stairs edit, steps half a metre down and a metre on).</summary>
    /// <param name="top">The top of the first step.</param>
    /// <param name="look">The way down (only its horizontal part counts).</param>
    /// <param name="steps">How many steps.</param>
    public static DigOp Stairs(Vector3 top, Vector3 look, int steps)
    {
        var flat = new Vector3(look.X, 0, look.Z);
        return Aimed(DigKind.Stairs, top, steps, RockMath.LengthSquared(flat) > 1e-4f ? flat : Vector3.UnitZ);
    }

    /// <summary>The middles of 2 m cubes covering a size around a middle: as many as it takes, centred on it.</summary>
    private static IEnumerable<float> Centred(float middle, float size)
    {
        int count = Math.Max(1, (int)Math.Ceiling(size / 2f));
        return Centres(middle - count, middle + count);
    }

    /// <summary>The box the edits lie in: the lowest and highest corners of their boxes and balls.</summary>
    /// <param name="ops">The edits (boxes and balls: their value a half size or a radius).</param>
    public static (Vector3 Min, Vector3 Max) Extent(IReadOnlyList<DigOp> ops) =>
        (new Vector3(ops.Min(o => o.Position.X - o.Value), ops.Min(o => o.Position.Y - o.Value), ops.Min(o => o.Position.Z - o.Value)),
         new Vector3(ops.Max(o => o.Position.X + o.Value), ops.Max(o => o.Position.Y + o.Value), ops.Max(o => o.Position.Z + o.Value)));

    /// <summary>The middles of cubes of a side's length filling a span (two metres by default), the last one ending at or past its end.</summary>
    private static IEnumerable<float> Centres(float from, float to, float side = 2f)
    {
        int count = Math.Max(1, (int)Math.Ceiling((to - from) / side));
        for (int i = 0; i < count; i++)
        {
            yield return from + (side * (i + 0.5f));
        }
    }

    /// <summary>A swing's direction made unit length; straight down when it has none.</summary>
    private static Vector3 Swing(Vector3 swing) => RockMath.LengthSquared(swing) < 0.001f ? -Vector3.UnitY : RockMath.Normalize(swing);
}
