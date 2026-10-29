using TerrainEditor.Editing;

namespace TerrainEditor.Desktop;

public sealed partial class Ground
{
    /// <summary>
    /// Same as TerrainComp.ApplyToHeightmap: the edited height never leaves the original ground ± 8 m
    /// (the original ground including the lift).
    /// </summary>
    public float HeightOf(int g) => Base[g] + Lift[g] + (Mod[g] != 0 ? Math.Clamp(Level[g] + Smooth[g], -EditStore.MaxLevel, EditStore.MaxLevel) : 0);

    /// <summary>The ground the game's ±8 m applies to: the generated one plus the lift.</summary>
    public float Original(int g) => Base[g] + Lift[g];

    /// <summary>
    /// The lift that takes the point back to the ground as the world generated it: under the ground discs saved No limit
    /// ground made (Base counts them as generated), 0 elsewhere. Restore backs the lift towards it.
    /// </summary>
    public float BareLift(int g) => _bare == null ? 0 : EditStore.KeepLift(_bare[g] - Base[g]);

    /// <summary>True when the point's change is at (within 5 cm of) the game's ±8 m.</summary>
    public bool AtLimit(int g) => Mod[g] != 0 && MathF.Abs(Level[g] + Smooth[g]) >= EditStore.MaxLevel - 0.05f;

    /// <summary>The outer line of points stays as it is, so the block always joins its neighbours.</summary>
    public bool Locked(int gx, int gz) => gx == 0 || gz == 0 || gx == W - 1 || gz == H - 1;

    /// <summary>
    /// Moves a point to a height (kept within the game's limit unless NoLimit); true when a limit stopped it: the
    /// point ends more than a millimetre from the height (the game's ±8 m, the level part's own ±8 m beside the
    /// smoothing, or No limit's lift).
    /// </summary>
    public bool SetHeight(int g, float h)
    {
        if (Mod[g] == 0)
        {
            Smooth[g] = 0;
            Level[g] = 0;
            Mod[g] = 1;
        }
        if (NoLimit)
        {
            // The edit keeps its level and smoothing; the lift takes the rest.
            float lift = Math.Clamp(h - Base[g] - Math.Clamp(Level[g] + Smooth[g], -EditStore.MaxLevel, EditStore.MaxLevel), -EditStore.MaxLift, EditStore.MaxLift);
            Lift[g] = MathF.Abs(lift) < 1e-3f ? 0 : lift;
        }
        else
        {
            float o = Original(g), target = Math.Clamp(h, o - EditStore.MaxLevel, o + EditStore.MaxLevel);
            // Keep the smoothing part and put the rest in the level part (itself limited to ± 8 m).
            Level[g] = Math.Clamp(target - o - Smooth[g], -EditStore.MaxLevel, EditStore.MaxLevel);
        }
        // Where it ended, whichever limit stopped it (a lift under a millimetre is dropped: not a stop).
        return MathF.Abs(HeightOf(g) - h) > 2e-3f;
    }

    /// <summary>
    /// The paint mask the game's terrain shader reads: the paint where painted, the generated one otherwise.
    /// </summary>
    public float MaskOf(int g, int c) => PMod[g] != 0 ? Paint[g * 4 + c] : BaseMask[g * 4 + c];

    /// <summary>
    /// Marks a point's paint as changed, starting from what it shows: the generated mask when it was not painted yet
    /// (dirt around a location stays dirt under a light first touch), else the paint it has.
    /// </summary>
    /// <param name="g">The ground point index.</param>
    public void StartPaint(int g)
    {
        if (PMod[g] == 0)
        {
            Array.Copy(BaseMask, g * 4, Paint, g * 4, 4);
            PMod[g] = 1;
        }
    }

    /// <summary>
    /// The editable arrays, for every point (Snapshot) or only some points (an undo step keeps just the ones it
    /// touched).
    /// </summary>
    public sealed record State(float[] Level, float[] Smooth, byte[] Mod, float[] Paint, byte[] PMod, float[] Lift);

    /// <summary>A copy of the editable arrays as they are now.</summary>
    public State Snapshot() => new((float[])Level.Clone(), (float[])Smooth.Clone(), (byte[])Mod.Clone(), (float[])Paint.Clone(), (byte[])PMod.Clone(), (float[])Lift.Clone());

    /// <summary>Every zone (block-relative) holding one of the points (edge points belong to two or four zones).</summary>
    public List<(int X, int Z)> ZonesOf(IEnumerable<int> points)
    {
        var set = new HashSet<(int, int)>();
        void Add(int zx, int zz)
        {
            if (zx >= 0 && zx < ZonesX && zz >= 0 && zz < ZonesZ)
            {
                set.Add((zx, zz));
            }
        }
        foreach (int g in points)
        {
            // The zone the point is in, and the one west (south) of it when the point is on their shared edge.
            int gx = g % W, gz = g / W;
            int x1 = gx / 64, x2 = (int)Math.Floor((gx - 1) / 64.0), z1 = gz / 64, z2 = (int)Math.Floor((gz - 1) / 64.0);
            Add(x1, z1);
            Add(x1, z2);
            Add(x2, z1);
            Add(x2, z2);
        }
        return [.. set.OrderBy(z => z.Item2).ThenBy(z => z.Item1)];
    }
}
