using TerrainEditor.Editing;
using TerrainService = ValheimGen.TerrainService;

namespace TerrainEditor.Desktop;

/// <summary>
/// The ground being edited, over the whole block of zones, the way the game keeps it (TerrainComp):
/// per grid point the generated height, the level and smoothing changes, whether the point was
/// changed, and the paint (dirt, cultivated, paved, vegetation) with whether it was painted. The same
/// arrays as the web editor's page (editor.html), turned back into zones for the EditStore (uploadNow).
/// Lift: height beyond the game's ±8 m (No limit), which saving turns into ground discs (Uplift): the
/// ground the ±8 m applies to is then the generated one plus the lift.
/// </summary>
public sealed partial class Ground
{
    /// <summary>
    /// Grid points across (ZonesX × 64 + 1 when read from zones: neighbouring zones share their edge points).
    /// </summary>
    public int W { get; }
    /// <summary>Grid points down (ZonesZ × 64 + 1), as <c>W</c>.</summary>
    public int H { get; }
    /// <summary>The block's first zone (world zone X).</summary>
    public int X0 { get; }
    /// <summary>The block's first zone (world zone Z).</summary>
    public int Z0 { get; }
    /// <summary>World x of grid point 0 (the block's west edge), as WorldScene.OriginX.</summary>
    public float OriginX => X0 * 64f - 32f;
    /// <summary>World z of grid point 0 (the block's south edge).</summary>
    public float OriginZ => Z0 * 64f - 32f;
    /// <summary>The block's size in zones, west to east.</summary>
    public int ZonesX { get; }
    /// <summary>The block's size in zones, south to north.</summary>
    public int ZonesZ { get; }
    /// <summary>The generated height per point, in metres.</summary>
    public float[] Base { get; }
    /// <summary>The level change per point (metres, used where <c>Mod</c> is set).</summary>
    public float[] Level { get; }
    /// <summary>The smoothing change per point (metres, used where <c>Mod</c> is set).</summary>
    public float[] Smooth { get; }
    /// <summary>
    /// The paint, four channels per point (dirt, cultivated, paved, vegetation), used where <c>PMod</c> is set.
    /// </summary>
    public float[] Paint { get; }
    /// <summary>The generated paint mask, four channels per point, shown where the point is not painted.</summary>
    public float[] BaseMask { get; }
    /// <summary>1 where the point's height was changed.</summary>
    public byte[] Mod { get; }
    /// <summary>1 where the point was painted.</summary>
    public byte[] PMod { get; }
    /// <summary>Height beyond the game's ±8 m per point (metres), saved as ground discs.</summary>
    public float[] Lift { get; }

    /// <summary>No limit: edits go past the game's ±8 m (the rest goes into Lift).</summary>
    public bool NoLimit { get; set; }

    /// <summary>
    /// The values as loaded (or last saved), and each zone's own saved values: zones share their edge
    /// points, and saved zones do not always agree on them. A point unchanged since loading is given
    /// back with the zone's own value, so an untouched zone stays identical to what is saved. Null until edits are
    /// taken (TakeEdits): the ground as made, every value 0.
    /// </summary>
    private State? _loaded;
    /// <summary>Each zone's own saved edit (block-relative zone; null when it had none), see <c>_loaded</c>.</summary>
    private readonly Dictionary<(int, int), ZoneEdit?> _own = [];

    /// <summary>An empty, unchanged ground of <paramref name="w"/> × <paramref name="h"/> points.</summary>
    /// <param name="w">Points across.</param>
    /// <param name="h">Points down.</param>
    /// <param name="x0">The block's first zone (X).</param>
    /// <param name="z0">The block's first zone (Z).</param>
    /// <param name="zonesX">The block's size in zones, west to east.</param>
    /// <param name="zonesZ">The block's size in zones, south to north.</param>
    public Ground(int w, int h, int x0, int z0, int zonesX, int zonesZ)
    {
        W = w;
        H = h;
        X0 = x0;
        Z0 = z0;
        ZonesX = zonesX;
        ZonesZ = zonesZ;
        int n = w * h;
        Base = new float[n];
        Level = new float[n];
        Smooth = new float[n];
        Mod = new byte[n];
        Paint = new float[n * 4];
        PMod = new byte[n];
        Lift = new float[n];
        BaseMask = new float[n * 4];
    }

    /// <summary>An empty, unchanged ground of a square block of <paramref name="size"/> × <paramref name="size"/> zones.</summary>
    /// <param name="w">Points across.</param>
    /// <param name="h">Points down.</param>
    /// <param name="x0">The block's first zone (X).</param>
    /// <param name="z0">The block's first zone (Z).</param>
    /// <param name="size">The block's size in zones, both ways.</param>
    public Ground(int w, int h, int x0, int z0, int size)
        : this(w, h, x0, z0, size, size)
    {
    }

    /// <summary>
    /// The block of <paramref name="zonesX"/> × <paramref name="zonesZ"/> zones from (x0, z0): generated ground plus its
    /// edits.
    /// </summary>
    public static Ground Read(TerrainService terrain, EditStore edits, int x0, int z0, int zonesX, int zonesZ)
    {
        var g = new Ground(zonesX * 64 + 1, zonesZ * 64 + 1, x0, z0, zonesX, zonesZ);
        g.ReadBase(terrain);
        g.TakeEdits(edits);
        return g;
    }

    /// <summary>The generated ground and paint (again when the ground discs changed: Uplift).</summary>
    public void ReadBase(TerrainService terrain)
    {
        for (int zz = 0; zz < ZonesZ; zz++)
        {
            for (int zx = 0; zx < ZonesX; zx++)
            {
                float[] b = terrain.BaseZone(X0 + zx, Z0 + zz);
                float[] m = terrain.BaseMask(X0 + zx, Z0 + zz);
                foreach (var (i, p) in ZoneGridPoints(zz, zx))
                {
                    Base[p] = b[i];
                    for (int c = 0; c < 4; c++)
                    {
                        BaseMask[p * 4 + c] = m[i * 4 + c];
                    }
                }
            }
        }
    }

    /// <summary>
    /// Each grid point of one zone: its index inside the zone's arrays and its index in the whole ground's.
    /// </summary>
    /// <param name="zz">The zone's row.</param>
    /// <param name="zx">The zone's column.</param>
    private IEnumerable<(int I, int P)> ZoneGridPoints(int zz, int zx)
    {
        for (int k = 0; k < EditStore.Grid; k++)
        {
            for (int l = 0; l < EditStore.Grid; l++)
            {
                yield return (k * EditStore.Grid + l, (zz * 64 + k) * W + zx * 64 + l);
            }
        }
    }

    /// <summary>A zone (block-relative) as the EditStore keeps it, in world zone coordinates.</summary>
    public ZoneEdit ZoneEdit(int zx, int zz)
    {
        var e = new ZoneEdit(X0 + zx, Z0 + zz);
        var own = _own.GetValueOrDefault((zx, zz));
        for (int k = 0; k < EditStore.Grid; k++)
        {
            for (int l = 0; l < EditStore.Grid; l++)
            {
                int g = (zz * 64 + k) * W + zx * 64 + l, i = k * EditStore.Grid + l;
                if (own != null && UnchangedSinceLoad(g))
                {
                    e.Modified[i] = own.Modified[i];
                    e.Level[i] = own.Level[i];
                    e.Smooth[i] = own.Smooth[i];
                    e.PaintModified[i] = own.PaintModified[i];
                    Array.Copy(own.Paint, i * 4, e.Paint, i * 4, 4);
                    e.Lift[i] = own.Lift[i];
                    continue;
                }
                e.Modified[i] = Mod[g] != 0;
                e.Level[i] = Level[g];
                e.Smooth[i] = Smooth[g];
                e.PaintModified[i] = PMod[g] != 0;
                Array.Copy(Paint, g * 4, e.Paint, i * 4, 4);
                e.Lift[i] = Lift[g];
            }
        }
        return e;
    }
}
