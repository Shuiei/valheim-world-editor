using System.Numerics;

namespace TerrainEditor.Tunnelheim;

/// <summary>
/// The shapes Tunnelheim's carving edits cut, as signed distances (negative inside the hole), worked out as the mod
/// works them out in the game (<see cref="RockMath"/>), and where each reaches.
/// </summary>
/// <remarks>Followed: Tunnelheim 1.0.0 (VoxelWorld.DigBounds, DigDistance, ChipShape, ChipFinish, Rough, Lattice; VoxelOp.PlaneNormal, FloorY).</remarks>
public static class DigShapes
{
    private const float DegToRad = 0.017453292f;

    /// <summary>The aimed direction of an edit (unit length), from its pitch and yaw.</summary>
    /// <param name="op">An aimed edit (chip, plane, stroke, stairs).</param>
    public static Vector3 Direction(in DigOp op)
    {
        float pitch = op.Kind == DigKind.ChipFloor ? ((op.Code & 0xFFFF) - 32768) / 100f : op.Code / 1000f;
        float yaw = op.Extra * DegToRad;
        float pitchRad = pitch * DegToRad;
        float c = (float)Math.Cos(pitchRad);
        return new Vector3((float)Math.Sin(yaw) * c, (float)Math.Sin(pitchRad), (float)Math.Cos(yaw) * c);
    }

    /// <summary>The height below which a <see cref="DigKind.ChipFloor"/> edit leaves the rock.</summary>
    /// <param name="op">The edit.</param>
    public static float FloorY(in DigOp op) => (float)(op.Position.Y + (double)(float)((op.Code >>> 16) - 32768) / 100.0);

    /// <summary>The sphere a carving edit lies in: its centre and radius.</summary>
    /// <param name="op">A carving edit.</param>
    public static (Vector3 Centre, float Radius) Bounds(in DigOp op)
    {
        switch (op.Kind)
        {
            case DigKind.Box:
                return (op.Position, op.Value * 1.42f);
            case DigKind.Stroke:
            {
                float length = StrokeLength(op.Value);
                return (op.Position + Direction(op) * (length * 0.5f), (float)(length * 0.5 + (double)0.85f * op.Value + 0.1f));
            }
            case DigKind.Stairs:
                return (op.Position + Direction(op) * (float)(op.Value * 0.5), (float)(op.Value * 0.5 + 1.0 + 2.5 + 0.5 * op.Value));
            case DigKind.Chip or DigKind.ChipFloor:
                return (op.Position, (float)((double)1.2f * op.Value + 0.1f));
            default:
                return (op.Position, op.Value);
        }
    }

    private static float StrokeLength(float size) => RockMath.Max(0f, 0.65f * size);

    /// <summary>The signed distance from a point to the hole an edit carves.</summary>
    /// <param name="op">A carving edit.</param>
    /// <param name="p">The point, in world metres.</param>
    public static float Distance(in DigOp op, Vector3 p) => op.Kind switch
    {
        DigKind.Stroke => StrokeDistance(op, p),
        DigKind.Stairs => StairsDistance(op, p),
        DigKind.Chip or DigKind.ChipFloor => Finish(op, p, Chip(op, p)),
        DigKind.Box => BoxDistance(op, p),
        _ => RockMath.Length(p - op.Position) - op.Value,
    };

    /// <summary>
    /// A round dig's ellipsoid, wide across its direction (1.2 times its size) and shallow along it (half its size),
    /// before its rough finish: never more than <see cref="Finish"/> lowers it by.
    /// </summary>
    /// <param name="op">A chip edit.</param>
    /// <param name="p">The point.</param>
    public static float Chip(in DigOp op, Vector3 p)
    {
        var dir = Direction(op);
        var v = p - op.Position;
        float along = RockMath.Dot(v, dir);
        float across = RockMath.LengthSquared(v - dir * along);
        float a = 1.2f * op.Value;
        float b = 0.5f * op.Value;
        float k0 = (float)Math.Sqrt((float)(across / ((double)a * a) + (double)along * along / ((double)b * b)));
        float k1 = (float)Math.Sqrt((float)(across / ((double)a * a * a * a) + (double)along * along / ((double)b * b * b * b)));
        return !(k1 < 1E-06f) ? (float)(k0 * (k0 - 1.0) / k1) : -b;
    }

    /// <summary>A round dig's finish: rough by up to 0.1 m, and (dig straight) nothing below its floor.</summary>
    /// <param name="op">A chip edit.</param>
    /// <param name="p">The point.</param>
    /// <param name="d">Its distance from <see cref="Chip"/>.</param>
    public static float Finish(in DigOp op, Vector3 p, float d)
    {
        d = (float)(d + (double)0.1f * Rough(p));
        return op.Kind != DigKind.ChipFloor ? d : RockMath.Max(d, FloorY(op) - p.Y);
    }

    private static float BoxDistance(in DigOp op, Vector3 p)
    {
        var q = Turn(-op.Extra, p - op.Position);
        var e = RockMath.AbsLess(q, new Vector3(op.Value, op.Value, op.Value));
        return RockMath.Length(RockMath.PositivePart(e)) + RockMath.Min(RockMath.Max(e.X, RockMath.Max(e.Y, e.Z)), 0f);
    }

    private static float StrokeDistance(in DigOp op, Vector3 p)
    {
        var dir = Direction(op);
        float t = RockMath.Clamp(RockMath.Dot(p - op.Position, dir), 0f, StrokeLength(op.Value));
        float m = RockMath.Length(p - (op.Position + dir * t));
        return (float)(m - (double)0.85f * op.Value + (double)0.1f * Rough(p));
    }

    private static float StairsDistance(in DigOp op, Vector3 p)
    {
        var dir = Direction(op);
        var side = new Vector3(dir.Z, 0f, -dir.X);
        var v = p - op.Position;
        float along = RockMath.Dot(v, dir);
        float across = RockMath.Dot(v, side);
        float best = float.MaxValue;
        for (int i = 0; i < (int)op.Value; i++)
        {
            float drop = -(i + 1) * 0.5f;
            var step = new Vector3((float)(along - (i + 0.5) * 1.0), (float)(v.Y - (drop + 1.25)), across);
            var e = RockMath.AbsLess(step, new Vector3(0.5f, 1.25f, 1f));
            float d = RockMath.Length(RockMath.PositivePart(e)) + RockMath.Min(RockMath.Max(e.X, RockMath.Max(e.Y, e.Z)), 0f);
            best = RockMath.Min(best, d);
        }
        return best;
    }

    /// <summary>
    /// A point turned about the vertical by an angle, as Unity's Quaternion.Euler(0, angle, 0) turns it (a turn of
    /// 0, which every edit Tunnelheim 1.0.0 makes has, leaves the point as it is).
    /// </summary>
    /// <param name="degrees">The angle, in degrees.</param>
    /// <param name="v">The point.</param>
    public static Vector3 Turn(float degrees, Vector3 v)
    {
#pragma warning disable S1244 // A zero turn is the identity, exactly.
        if (degrees == 0f)
#pragma warning restore S1244
        {
            return v;
        }
        float half = degrees * DegToRad * 0.5f;
        float qy = MathF.Sin(half), qw = MathF.Cos(half);
        float y2 = qy * 2f;
        float yy = qy * y2, wy = qw * y2;
        return new Vector3(
            (float)((1.0 - yy) * v.X + (double)wy * v.Z),
            v.Y,
            (float)(-(double)wy * v.X + (1.0 - yy) * v.Z));
    }

    /// <summary>Value noise of period 1 m, -1 to 1, smoothed between whole-metre corners (the dig's rough finish).</summary>
    /// <param name="p">The point.</param>
    public static float Rough(Vector3 p)
    {
        float fx = p.X / 1f, fy = p.Y / 1f, fz = p.Z / 1f;
        int x = RockMath.Floor(fx), y = RockMath.Floor(fy), z = RockMath.Floor(fz);
        float tx = Smooth(fx - x), ty = Smooth(fy - y), tz = Smooth(fz - z);
        float a = RockMath.Lerp(Lattice(x, y, z), Lattice(x + 1, y, z), tx);
        float b = RockMath.Lerp(Lattice(x, y + 1, z), Lattice(x + 1, y + 1, z), tx);
        float c = RockMath.Lerp(Lattice(x, y, z + 1), Lattice(x + 1, y, z + 1), tx);
        float d = RockMath.Lerp(Lattice(x, y + 1, z + 1), Lattice(x + 1, y + 1, z + 1), tx);
        return RockMath.Lerp(RockMath.Lerp(a, b, ty), RockMath.Lerp(c, d, ty), tz);
    }

    private static float Smooth(float t) => (float)((double)t * t * (3.0 - 2.0 * t));

    /// <summary>The noise's value at a whole-metre corner, from a hash of its coordinates.</summary>
    private static float Lattice(int x, int y, int z)
    {
        unchecked
        {
            int h = (x * -1918454973) ^ (y * -669632447) ^ (z * -887442657);
            h = (h ^ (h >>> 15)) * 739982445;
            h = (h ^ (h >>> 12)) * 695872825;
            return (float)((uint)((h ^ (h >>> 15)) & 0xFFFF) / 32767.5 - 1.0);
        }
    }
}
