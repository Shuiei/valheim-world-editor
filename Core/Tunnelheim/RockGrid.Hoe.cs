using System.Numerics;

namespace TerrainEditor.Tunnelheim;

public sealed partial class RockGrid
{
    /// <summary>
    /// A hoe or cultivator stroke on rock (VoxelWorld.ApplyTerrainOp): within its reach, each voxelised column's rock
    /// surface nearest the stroke's height is levelled, raised or smoothed as the game's terrain operation does it to
    /// the ground, then painted. Nothing happens unless a voxelised column lies within the reach.
    /// </summary>
    /// <param name="pos">Where the stroke was made.</param>
    /// <param name="turn">Its turn about the vertical, in degrees (square paint follows it).</param>
    /// <param name="s">The terrain operation's settings.</param>
    /// <param name="clip">The only column written, or null for all.</param>
    /// <param name="changed">Collects the columns whose rock changed, when given.</param>
    /// <returns>Whether a sample changed.</returns>
    public bool Hoe(Vector3 pos, float turn, HoeSettings s, (int X, int Z)? clip = null, ISet<(int X, int Z)>? changed = null)
    {
        float reach = s.Radius() + 0.5f;
        int x0 = RockMath.FloorStep(pos.X - reach), x1 = RockMath.CeilStep(pos.X + reach);
        int z0 = RockMath.FloorStep(pos.Z - reach), z1 = RockMath.CeilStep(pos.Z + reach);
        if (!EnsureHoeChunks(pos, x0, z0, x1, z1, clip))
        {
            return false;
        }
        var stroke = new HoeStroke(pos, turn, s, (float)(pos.Y + (double)s.LevelOffset), PaintOf(s, pos), IsConfined(pos));
        bool any = false;
        for (int gz = z0; gz <= z1; gz++)
        {
            for (int gx = x0; gx <= x1; gx++)
            {
                if (IsColumn(FloorDiv(gx, Samples), FloorDiv(gz, Samples)) && InClip(gx, gz, clip) && HoePoint(stroke, gx, gz))
                {
                    any = true;
                    changed?.Add(ColumnOf(gx, gz));
                }
            }
        }
        return any;
    }

    /// <summary>What one stroke does at every point.</summary>
    private readonly record struct HoeStroke(Vector3 Pos, float Turn, HoeSettings S, float Target, byte Paint, bool Confined);

    /// <summary>Makes the chunks 10 m above and below the stroke in each voxelised column it reaches; false when none.</summary>
    private bool EnsureHoeChunks(Vector3 pos, int x0, int z0, int x1, int z1, (int X, int Z)? clip)
    {
        int lo = FloorDiv(RockMath.FloorStep((float)(pos.Y - 8.0 - 2.0)), Samples);
        int hi = FloorDiv(RockMath.CeilStep((float)(pos.Y + 8.0 + 2.0)), Samples);
        var (cx0, cz0) = ColumnOf(x0, z0);
        var (cx1, cz1) = ColumnOf(x1, z1);
        bool any = false;
        for (int cz = cz0; cz <= cz1; cz++)
        {
            for (int cx = cx0; cx <= cx1; cx++)
            {
                if (!IsColumn(cx, cz) || (clip is { } c && c != (cx, cz)))
                {
                    continue;
                }
                any = true;
                for (int cy = lo; cy <= hi; cy++)
                {
                    EnsureChunk(cx, cy, cz);
                }
            }
        }
        return any;
    }

    /// <summary>
    /// The rock paint a stroke lays (VoxelWorld.PaintCode), or 0 for none: in the Deep North, only a cultivator lays
    /// cultivated or snow paint.
    /// </summary>
    private byte PaintOf(HoeSettings s, Vector3 pos)
    {
        byte code = s.PaintType switch
        {
            0 => RockPaint.Dirt,
            1 => RockPaint.Cultivated,
            2 => RockPaint.Paved,
            4 => RockPaint.NoVegetation,
            _ => RockPaint.Natural,
        };
        bool snow = s.PaintType is 1 or 5 && DeepNorth(pos.X, pos.Z);
        bool cultivator = s.PaintCleared && s.PaintType == 1 && (s.Level || s.Raise || s.Smooth);
        return snow && !cultivator ? (byte)0 : code;
    }

    /// <summary>The stroke at one grid point: the new surface height, then the paint.</summary>
    private bool HoePoint(in HoeStroke k, int gx, int gz)
    {
        var s = k.S;
        float dx = gx * Step - k.Pos.X;
        float dz = gz * Step - k.Pos.Z;
        float dist = (float)Math.Sqrt((float)((double)dx * dx + (double)dz * dz));
        float h = SurfaceNear(gx, gz, k.Pos.Y);
        if (k.Confined && h > k.Pos.Y + 1.0)
        {
            return false;
        }
        float t = Reshaped(k, dist, dx, dz, h);
        bool any = MathF.Abs(t - h) > 0.001f && SetSurface(gx, gz, h, t);
        if (s.PaintCleared && k.Paint != 0 && PaintReach(k, dist, dx, dz))
        {
            float hp = SurfaceNear(gx, gz, t);
            any |= MathF.Abs(hp - t) <= 1f && SetPaint(gx, gz, hp, k.Paint);
        }
        return any;
    }

    /// <summary>The surface height a stroke gives a point whose rock surface is at h: levelled, raised, smoothed, held to 8 m.</summary>
    private static float Reshaped(in HoeStroke k, float dist, float dx, float dz, float h)
    {
        var s = k.S;
        float t = h;
        if (s.Level && InReach(s.Square, dist, dx, dz, s.LevelRadius))
        {
            t = k.Target;
        }
        if (s.Raise && InReach(s.Square, dist, dx, dz, s.RaiseRadius))
        {
            t = Raised(s, k.Pos.Y, dist, t);
        }
        if (s.Smooth && dist <= s.SmoothRadius)
        {
            float w = (float)Math.Pow(dist / s.SmoothRadius, s.SmoothPower);
            t = RockMath.Clamp(RockMath.Lerp(t, k.Target, 1f - w), t - 1f, t + 1f);
        }
        return RockMath.Clamp(t, h - 8f, h + 8f);
    }

    private static bool InReach(bool square, float dist, float dx, float dz, float radius) =>
        square ? MathF.Abs(dx) <= radius && MathF.Abs(dz) <= radius : dist <= radius;

    private static float Raised(HoeSettings s, float y, float dist, float t)
    {
        float f = 1f;
        if (!s.Square && s.RaisePower > 0f)
        {
            f = (float)(1.0 - (double)dist / s.RaiseRadius);
#pragma warning disable S1244 // The game tests the power for exactly 1.
            if (s.RaisePower != 1f)
#pragma warning restore S1244
            {
                f = (float)Math.Pow(f, s.RaisePower);
            }
        }
        float delta = s.RaiseDelta * f;
        float raised = y + delta;
        if ((s.RaiseDelta < 0f && raised > t) || (s.RaiseDelta > 0f && raised < t))
        {
            return t;
        }
        return s.RaiseDelta > 0f ? RockMath.Min(raised, t + delta) : raised;
    }

    private static bool PaintReach(in HoeStroke k, float dist, float dx, float dz)
    {
        if (!k.S.Square)
        {
            return dist <= k.S.PaintRadius;
        }
        var turned = DigShapes.Turn(-k.Turn, new Vector3(dx, 0f, dz));
        return MathF.Abs(turned.X) <= k.S.PaintRadius && MathF.Abs(turned.Z) <= k.S.PaintRadius;
    }

    /// <summary>
    /// The rock surface nearest a height in a column of samples, within 8 m (VoxelWorld.SurfaceNear): where rock with
    /// air above crosses zero, the higher one on a tie; with none, 8 m above when the height is in rock, else 8 m below.
    /// </summary>
    /// <param name="gx">The column's grid x.</param>
    /// <param name="gz">Its grid z.</param>
    /// <param name="refY">The height.</param>
    public float SurfaceNear(int gx, int gz, float refY)
    {
        int top = RockMath.CeilStep((float)(refY + 8.0));
        int bottom = RockMath.FloorStep((float)(refY - 8.0));
        float found = float.NaN;
        float best = float.MaxValue;
        float above = DensityAt(gx, top, gz);
        for (int gy = top - 1; gy >= bottom; gy--)
        {
            float d = DensityAt(gx, gy, gz);
            if (d > 0f && above <= 0f)
            {
                float y = (float)((gy + d / ((double)d - above)) * 0.5);
                float off = MathF.Abs(y - refY);
                if (off < best)
                {
                    best = off;
                    found = y;
                }
            }
            above = d;
        }
        if (!float.IsNaN(found))
        {
            return found;
        }
        return DensityAt(gx, RockMath.Round(refY / Step), gz) > 0f ? refY + 8f : refY - 8f;
    }

    /// <summary>
    /// Moves the rock surface of one column of samples from h to t (VoxelWorld.SetSurface), in chunks that are there:
    /// lowering takes rock away above t, raising adds it up to t; nothing opens below <see cref="MaxDepth"/>.
    /// </summary>
    private bool SetSurface(int gx, int gz, float h, float t)
    {
        float seed = SeedAt(gx, gz);
        bool lower = t < h;
        int y0 = lower ? RockMath.FloorStep(t) : RockMath.FloorStep(h) - 1;
        int y1 = lower ? RockMath.CeilStep(h) + 1 : RockMath.CeilStep(t);
        bool any = false;
        for (int gy = y0; gy <= y1; gy++)
        {
            float y = gy * Step;
            var chunk = Chunk(FloorDiv(gx, Samples), FloorDiv(gy, Samples), FloorDiv(gz, Samples));
            if (chunk == null)
            {
                continue;
            }
            int i = IndexIn(gx, gy, gz);
            float old = chunk[i];
            float nv = lower ? RockMath.Min(old, t - y) : RockMath.Max(old, t - y);
            if ((!(nv <= 0f) || !(old > 0f) || MayOpen(y, seed)) && MathF.Abs(nv - old) > 0.0001f)
            {
                chunk[i] = nv;
                any = true;
            }
        }
        return any;
    }

    /// <summary>
    /// Whether a point is under rock (VoxelWorld.IsConfined): going up from half a metre above it to 10 m, rock comes
    /// after air.
    /// </summary>
    private bool IsConfined(Vector3 pos)
    {
        int gx = RockMath.Round(pos.X / Step);
        int gz = RockMath.Round(pos.Z / Step);
        int y0 = RockMath.CeilStep((float)(pos.Y + 0.5));
        int y1 = RockMath.CeilStep((float)(pos.Y + 10.0));
        bool air = false;
        for (int gy = y0; gy <= y1; gy++)
        {
            if (!(DensityAt(gx, gy, gz) > 0f))
            {
                air = true;
            }
            else if (air)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>Paints the samples a metre above and below a height in one column of samples (VoxelWorld.SetPaint).</summary>
    private bool SetPaint(int gx, int gz, float y, byte code)
    {
        int y0 = RockMath.FloorStep((float)(y - 1.0));
        int y1 = RockMath.CeilStep((float)(y + 1.0));
        bool any = false;
        for (int gy = y0; gy <= y1; gy++)
        {
            var chunk = Chunk(FloorDiv(gx, Samples), FloorDiv(gy, Samples), FloorDiv(gz, Samples));
            int i = IndexIn(gx, gy, gz);
            if (chunk != null && chunk.PaintAt(i) != code)
            {
                chunk.Paint(i, code);
                any = true;
            }
        }
        return any;
    }
}
