using TerrainEditor.App;
using TerrainEditor.Editing;

namespace TerrainEditor.Desktop;

public sealed partial class WorldScene
{
    /// <summary>Opens the world in <paramref name="dir"/> and reads an area of it (see the other overload).</summary>
    public static WorldScene Load(string dir, int zx, int zz, int size)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var owner = WorldSession.Open(dir);
        return Load(owner, zx, zz, size, watch.ElapsedMilliseconds);
    }

    /// <summary>
    /// An area (size × size zones around zone zx, zz) of an open world, with its edit session; the world's kept
    /// history is fitted to it, and it becomes the world's current area.
    /// </summary>
    /// <param name="owner">The open world.</param>
    /// <param name="zx">The middle zone, x.</param>
    /// <param name="zz">The middle zone, z.</param>
    /// <param name="size">Zones across.</param>
    /// <param name="readMs">How long the save took to read, for <c>LoadInfo</c>.</param>
    public static WorldScene Load(WorldSession owner, int zx, int zz, int size, long readMs = 0) =>
        LoadBlock(owner, zx - size / 2, zz - size / 2, size, size, readMs);

    /// <summary>
    /// An area (zonesX × zonesZ zones from its south-west zone x0, z0) of an open world, with its edit session; the
    /// world's kept history is fitted to it, and it becomes the world's current area.
    /// </summary>
    /// <param name="owner">The open world.</param>
    /// <param name="x0">The south-west zone, x.</param>
    /// <param name="z0">The south-west zone, z.</param>
    /// <param name="zonesX">Zones west to east.</param>
    /// <param name="zonesZ">Zones south to north.</param>
    /// <param name="readMs">How long the save took to read, for <c>LoadInfo</c>.</param>
    public static WorldScene LoadBlock(WorldSession owner, int x0, int z0, int zonesX, int zonesZ, long readMs = 0)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var world = owner.World;
        var edits = owner.Edits;
        var modifiers = owner.Modifiers;
        var terrain = owner.Terrain;
        int x1 = x0 + zonesX - 1, z1 = z0 + zonesZ - 1;
        // The zones' generated ground and paint mask in parallel, one pass each (the slow part), then the grid with the
        // edits.
        var keys = (from z in Enumerable.Range(z0, zonesZ) from x in Enumerable.Range(x0, zonesX) select (x, z)).ToArray();
        Parallel.ForEach(keys, k => terrain.Warm(k.x, k.z));
        var (w, h, heights) = HeightGrid.Read(terrain, edits, x0, z0, x1, z1);
        int[] biomes = new int[w * h];
        byte[] biomeCol = new byte[w * h * 4], mask = new byte[w * h * 4];
        float[] limit = new float[w * h], ocean = new float[w * h];
        static float Smooth01(float t) => t * t * (3 - 2 * t);
        foreach (var (x, z) in keys)
        {
            int[] b = terrain.VertexBiomes(x, z);
            float[] baseMask = terrain.BaseMask(x, z);
            byte[][] corners = [.. terrain.CornerBiomes(x, z).Select(BiomeRgba)];
            var e = edits.Get(x, z);
            int ox = (x - x0) * 64, oz = (z - z0) * 64;
            for (int k = 0; k < 65; k++)
            {
                for (int l = 0; l < 65; l++)
                {
                    int i = k * 65 + l, g = (oz + k) * w + ox + l;
                    biomes[g] = b[i];
                    float ix = Smooth01(l / 64f), iy = Smooth01(k / 64f);
                    for (int c = 0; c < 4; c++)
                    {
                        // Integer steps like the web editor (Math.trunc), so both look the same.
                        int lo = (int)(corners[0][c] + (corners[1][c] - corners[0][c]) * ix), hi = (int)(corners[2][c] + (corners[3][c] - corners[2][c]) * ix);
                        biomeCol[g * 4 + c] = (byte)(int)(lo + (hi - lo) * iy);
                        float m = e != null && e.PaintModified[i] ? e.Paint[i * 4 + c] : baseMask[i * 4 + c];
                        mask[g * 4 + c] = (byte)Math.Clamp((int)MathF.Round(m * 255f), 0, 255);
                    }
                    limit[g] = e != null && e.Modified[i] && MathF.Abs(e.Level[i] + e.Smooth[i]) >= EditStore.MaxLevel - 0.05f ? 1 : 0;
                }
            }
        }
        // Heightmap.UpdateCornerDepths: each zone gets max(0, 30 - height) at its corners, blended across it.
        for (int zz2 = 0; zz2 < zonesZ; zz2++)
        {
            for (int zx2 = 0; zx2 < zonesX; zx2++)
            {
                float D(int gx, int gz) => MathF.Max(0, 30 - heights[gz * w + gx]);
                int ax = zx2 * 64, az = zz2 * 64;
                float d00 = D(ax, az), d10 = D(ax + 64, az), d01 = D(ax, az + 64), d11 = D(ax + 64, az + 64);
                for (int k = 0; k <= 64; k++)
                {
                    for (int l = 0; l <= 64; l++)
                    {
                        float u = l / 64f, v = k / 64f;
                        ocean[(az + k) * w + ax + l] = (d00 + (d10 - d00) * u) * (1 - v) + (d01 + (d11 - d01) * u) * v;
                    }
                }
            }
        }
        float minX = x0 * 64f - 32f, minZ = z0 * 64f - 32f;
        var things = ReadThings(world, x0, z0, zonesX, zonesZ, edits.Deleted, edits.Added);
        var scene = new WorldScene
        {
            World = world,
            Name = world.Name,
            X0 = x0,
            Z0 = z0,
            ZonesX = zonesX,
            ZonesZ = zonesZ,
            W = w,
            H = h,
            Heights = heights,
            Biomes = biomes,
            Cx = minX + (w - 1) / 2f,
            Cz = minZ + (h - 1) / 2f,
            Things = things,
            BiomeColor = biomeCol,
            Mask = mask,
            OceanDepth = ocean,
            Limit = limit,
            Modifiers = modifiers,
            Terrain = terrain,
            Owner = owner,
            LoadInfo = Lang.T("world.loadInfo", world.Name, readMs, zonesX, zonesZ, things.Count, watch.ElapsedMilliseconds),
        };
        scene.Session = new EditSession(scene, Ground.Read(terrain, edits, x0, z0, zonesX, zonesZ), edits);
        owner.Area = scene.Session;
        // The world's history (made in other areas), fitted to this one.
        if (owner.History is { } kept)
        {
            scene.Session.Import(kept, ids => FindThings(world, edits, ids));
        }
        return scene;
    }
}
