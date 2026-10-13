using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using TerrainEditor.Terrain;
using TerrainService = ValheimGen.TerrainService;
using Vector3 = System.Numerics.Vector3;

namespace TerrainEditor.Desktop;

/// <summary>
/// What the 3D view shows: a rectangular block of zones of a world, its ground (heights and biomes, edits
/// included) and the objects and building pieces in it. Read once from the save through the shared
/// library, the same way the web editor's session does.
/// </summary>
/// <remarks>
/// Coordinates: the grid starts at the block's south-west corner (one point per metre, 64 per zone,
/// shared edges once); the 3D view puts the block's middle at 0 and mirrors z (OpenGL is right-handed,
/// Unity left-handed), like the web editor: view = (x - Cx, y, -(z - Cz)) in world metres.
/// </remarks>
public sealed class WorldScene
{
    /// <summary>The save the block was read from. Replaced by a fresh read after saving.</summary>
    public required WorldSave World { get; set; }

    /// <summary>The Workshop's: the blueprint open was written as a ruin (no builder), and is saved as one.</summary>
    public bool Ruin { get; set; }
    /// <summary>The world's name (the Workshop's title for the plot).</summary>
    public required string Name { get; init; }
    /// <summary>The block's south-west zone, x.</summary>
    public int X0 { get; init; }
    /// <summary>The block's south-west zone, z.</summary>
    public int Z0 { get; init; }
    /// <summary>The block's size in zones, west to east.</summary>
    public int ZonesX { get; init; }
    /// <summary>The block's size in zones, south to north.</summary>
    public int ZonesZ { get; init; }
    /// <summary>Grid points west to east (ZonesX × 64 + 1).</summary>
    public int W { get; init; }
    /// <summary>Grid points south to north (ZonesZ × 64 + 1).</summary>
    public int H { get; init; }
    /// <summary>The ground height in metres per grid point, edits included, row by row from the south (z × W + x).</summary>
    public required float[] Heights { get; init; }
    /// <summary>The biome (Heightmap.Biome value) per grid point, laid out like Heights.</summary>
    public required int[] Biomes { get; init; }
    /// <summary>World x of the block's middle, in metres.</summary>
    public float Cx { get; init; }
    /// <summary>World z of the block's middle, in metres.</summary>
    public float Cz { get; init; }
    /// <summary>The sea level in metres.</summary>
    public float Water { get; init; } = TerrainService.WaterLevel;
    /// <summary>
    /// The objects and building pieces in the block; only ever added to until the world is read again (see
    /// <see cref="Thing"/>). Lock it to read.
    /// </summary>
    public required List<Thing> Things { get; init; }
    /// <summary>The ground the world's locations and pieces flatten (for the Location flattening overlay).</summary>
    public TerrainModifiers? Modifiers { get; set; }
    /// <summary>The world generator's ground (regrow asks it what grows where); null in tests that only draw.</summary>
    public TerrainService? Terrain { get; init; }
    /// <summary>
    /// For the game's terrain shader, per grid point: the biome colour the game puts in its mesh (corner
    /// biomes blended like Heightmap.GetBiomeColor, RGBA bytes).
    /// </summary>
    public required byte[] BiomeColor { get; init; }
    /// <summary>
    /// The paint mask per grid point, RGBA bytes (_ClearedMaskTex: the paint where it was edited, the generated
    /// ground's otherwise).
    /// </summary>
    public required byte[] Mask { get; init; }
    /// <summary>The depth below sea level the game gives each zone's corners (blended across the zone), per grid point.</summary>
    public required float[] OceanDepth { get; init; }
    /// <summary>1 at grid points at the ±8 m edit limit, 0 elsewhere.</summary>
    public required float[] Limit { get; init; }
    /// <summary>A line on how the block was read (timings, zones, objects), for the status bar.</summary>
    public string LoadInfo { get; init; } = "";
    /// <summary>Editing the ground (null in tests that only draw).</summary>
    public EditSession? Session { get; set; }
    /// <summary>The open world this area belongs to (shared by every area opened from the map); null in the Workshop.</summary>
    public WorldSession? Owner { get; init; }

    /// <summary>
    /// An object or building piece. Things are only ever added to the list, so indices stay valid until the
    /// world is read again (after a save or a reload, EditSession.Reread fills the list anew and the history
    /// starts over): a deleted one is kept, Gone, so undo can bring it back; a moved one is a new thing.
    /// </summary>
    /// <param name="Id">The object's id in the save; negative for one added in this session.</param>
    /// <param name="Prefab">The prefab's hash.</param>
    /// <param name="Position">World position in metres.</param>
    /// <param name="Rotation">Euler angles in degrees, as stored (a building piece read from the save turns about y only).</param>
    /// <param name="Scale">Uniform scale; zero when the prefab's own scale applies.</param>
    /// <param name="Piece">A building piece (a building tool's) rather than a plain object.</param>
    public readonly record struct Thing(int Id, int Prefab, Vector3 Position, Vector3 Rotation, float Scale, bool Piece)
    {
        /// <summary>Deleted (or added and undone): kept so undo can bring it back, not drawn.</summary>
        public bool Gone { get; init; }

        /// <summary>A creature a player tamed (its own View kind).</summary>
        public bool Tamed { get; init; }

        /// <summary>
        /// A ruin's piece (no builder) joined to a player's building (JoinedRuins): shown with Your buildings.
        /// </summary>
        public bool Joined { get; init; }

        /// <summary>
        /// A runestone: in the save a location proxy, listed under the location's name. It can be
        /// picked and deleted, not moved or copied (see PrefabCatalog.RunestoneLocations).
        /// </summary>
        public bool Runestone => PrefabCatalog.IsRunestone(Prefab);
    }

    /// <summary>
    /// A thing as drawn: a part of a ValheimRAFT vehicle where the mod builds it (at its offset from its parent, the
    /// parent where its own parent puts it...), its root turned as stored (leaning too). Others are left as they are.
    /// What is stored stays: the game keeps a vehicle's parts at its root.
    /// </summary>
    /// <param name="t">The thing, as read from the save or made for an added object.</param>
    /// <param name="vehicles">The vehicles as they are now.</param>
    public static Thing Placed(Thing t, VehicleIndex vehicles) =>
        vehicles.Pose(t.Id) is var (position, rotation) ? t with { Position = position, Rotation = rotation } : t;

    /// <summary>A new object is tamed when it is a moved tamed creature (a move keeps the object's own data).</summary>
    public static bool TamedOf(WorldSave world, NewObject n) => !n.Fresh && n.SourceId is int s && world.Tamed.Contains(s);

    /// <summary>The game's biome colour for a biome (Heightmap.GetBiomeColor): which terrain textures it blends, as RGBA bytes.</summary>
    internal static byte[] BiomeRgba(int biome) => biome switch
    {
        2 => [255, 0, 0, 0],
        4 => [0, 255, 0, 0],
        8 => [0, 0, 255, 0],
        16 => [0, 0, 0, 255],
        32 => [255, 0, 0, 255],
        64 => [0, 255, 0, 0],
        512 => [0, 0, 255, 255],
        _ => [0, 0, 0, 0],
    };

    /// <summary>
    /// The world's folder: a path, a world's name, or (null) the one saved last on this computer. Throws
    /// <c>InvalidOperationException</c> when there is no such world.
    /// </summary>
    public static string FindWorld(string? want)
    {
        if (want != null && Directory.Exists(want))
        {
            return Path.GetFullPath(want);
        }
        var all = Worlds.Find(AppSettings.Load()).Where(w => w.Usable).ToList();
        var pick = want == null ? all.FirstOrDefault() : all.FirstOrDefault(w => string.Equals(w.Name, want, StringComparison.OrdinalIgnoreCase));
        return pick?.Path ?? throw new InvalidOperationException(want == null ? "No Valheim world found on this computer." : $"No world called {want}.");
    }

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
        // The zones' generated ground in parallel (the slow part), then the grid with the edits.
        var keys = (from z in Enumerable.Range(z0, zonesZ) from x in Enumerable.Range(x0, zonesX) select (x, z)).ToArray();
        Parallel.ForEach(keys, k => terrain.BaseZone(k.x, k.z));
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
        float minX = x0 * 64f - 32f, maxX = x1 * 64f + 32f, minZ = z0 * 64f - 32f, maxZ = z1 * 64f + 32f;
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
            LoadInfo = $"{world.Name}: read in {readMs} ms, {zonesX}×{zonesZ} zones and {things.Count:N0} objects ready in {watch.ElapsedMilliseconds} ms",
        };
        scene.Session = new EditSession(scene, Ground.Read(terrain, edits, x0, z0, zonesX, zonesZ), edits);
        owner?.Area = scene.Session;
        // The world's history (made in other areas), fitted to this one.
        if (owner?.History is { } kept)
        {
            scene.Session.Import(kept, ids => FindThings(world, edits, ids));
        }
        return scene;
    }

    /// <summary>
    /// Objects by id wherever they are, as things gone or not (deleted, or added and undone, they are gone):
    /// what a history fitted to an area needs that the area does not show. Ids not found are left out.
    /// </summary>
    public static Dictionary<int, Thing> FindThings(WorldSave world, EditStore edits, IReadOnlyCollection<int> ids)
    {
        lock (world.Sync)
        {
            return FindThingsIn(world, edits, ids);
        }
    }

    /// <summary><c>FindThings</c>' work, with the world's lock held.</summary>
    private static Dictionary<int, Thing> FindThingsIn(WorldSave world, EditStore edits, IReadOnlyCollection<int> ids)
    {
        var want = ids.ToHashSet();
        var deleted = edits.Deleted;
        var addedNow = edits.Added;
        var added = addedNow.Select(n => n.Id).ToHashSet();
        var vehicles = VehicleIndex.Of(world, deleted, addedNow);
        var found = new Dictionary<int, Thing>();
        foreach (int id in want.Where(i => i < 0))
        {
            if (edits.FindAdded(id) is { } n)
            {
                found[id] = Placed(new Thing(n.Id, n.Prefab, n.Position, n.Rotation, n.Scale, PieceCatalog.Get(n.Prefab)?.Tool != null) { Gone = !added.Contains(id), Tamed = TamedOf(world, n) }, vehicles);
            }
        }
        if (want.Any(i => i >= 0))
        {
            foreach (var (id, prefab, p, r, sc) in world.Objects)
            {
                if (want.Contains(id))
                {
                    found[id] = Placed(new Thing(id, prefab, p, r, sc.X, false) { Gone = deleted.Contains(id), Tamed = world.Tamed.Contains(id) }, vehicles);
                }
            }
            foreach (var (id, prefab, p, ry) in world.Pieces)
            {
                if (want.Contains(id))
                {
                    found[id] = Placed(new Thing(id, prefab, p, new Vector3(0, ry, 0), 0, true) { Gone = deleted.Contains(id) }, vehicles);
                }
            }
            // Live: removed in the game since (gone for good, but still in the history).
            foreach (int id in want.Where(i => i >= 0 && i < world.ObjectRefs.Count && world.Vanished.Contains(i) && !found.ContainsKey(i)))
            {
                var o = world.ObjectRefs[id];
                found[id] = new Thing(id, o.Prefab, o.Position, Vector3.Zero, 0, o.IsPiece) { Gone = true };
            }
        }
        return found;
    }

    /// <summary>The objects and building pieces of the block's zones (those not deleted).</summary>
    /// <param name="world">The save.</param>
    /// <param name="x0">The block's south-west zone, x.</param>
    /// <param name="z0">The block's south-west zone, z.</param>
    /// <param name="zonesX">Zones west to east.</param>
    /// <param name="zonesZ">Zones south to north.</param>
    /// <param name="deleted">Ids of deleted objects, left out.</param>
    /// <param name="added">Objects added in this session (other areas of the world may have placed or moved some here).</param>
    public static List<Thing> ReadThings(WorldSave world, int x0, int z0, int zonesX, int zonesZ, ICollection<int> deleted, IEnumerable<NewObject>? added = null)
    {
        lock (world.Sync)
        {
            return ReadThingsIn(world, x0, z0, zonesX, zonesZ, deleted, added);
        }
    }

    /// <summary><c>ReadThings</c>' work, with the world's lock held.</summary>
    private static List<Thing> ReadThingsIn(WorldSave world, int x0, int z0, int zonesX, int zonesZ, ICollection<int> deleted, IEnumerable<NewObject>? added)
    {
        float minX = x0 * 64f - 32f, maxX = (x0 + zonesX - 1) * 64f + 32f, minZ = z0 * 64f - 32f, maxZ = (z0 + zonesZ - 1) * 64f + 32f;
        bool Inside(Vector3 p) => p.X >= minX && p.X < maxX && p.Z >= minZ && p.Z < maxZ;
        var things = new List<Thing>();
        List<NewObject> addedNow = [.. added ?? []];
        var vehicles = VehicleIndex.Of(world, deleted, addedNow);
        foreach (var (id, prefab, p, r, sc) in world.Objects)
        {
            if (Inside(p) && !deleted.Contains(id))
            {
                things.Add(Placed(new Thing(id, prefab, p, r, sc.X, false) { Tamed = world.Tamed.Contains(id) }, vehicles));
            }
        }
        foreach (var (id, prefab, p, ry) in world.Pieces)
        {
            if (Inside(p) && !deleted.Contains(id))
            {
                things.Add(Placed(new Thing(id, prefab, p, new Vector3(0, ry, 0), 0, true), vehicles));
            }
        }
        // Not the ground discs for No limit ground (invisible: they are just the ground).
        foreach (var n in addedNow)
        {
            if (Inside(n.Position) && n.Prefab != WorldSave.LocationProxyPrefab)
            {
                things.Add(Placed(new Thing(n.Id, n.Prefab, n.Position, n.Rotation, n.Scale, PieceCatalog.Get(n.Prefab)?.Tool != null) { Tamed = TamedOf(world, n) }, vehicles));
            }
        }
        // The ruins' pieces joined to a building (the world's and this session's built pieces), found over the whole
        // world: the building may be just outside the area.
        var joined = JoinedRuins.InWorld(world, deleted, addedNow.Where(n => PieceCatalog.Get(n.Prefab)?.Tool != null).Select(n => n.Position));
        for (int i = 0; i < things.Count; i++)
        {
            if (!things[i].Piece && joined.Contains(things[i].Id))
            {
                things[i] = things[i] with { Joined = true };
            }
        }
        return things;
    }

    /// <summary>
    /// After an edit: the heights, paint mask and limit marks of a rectangle of grid points from the ground
    /// (grid points x0..x1, z0..z1, inclusive).
    /// </summary>
    public void Refresh(Ground g, int x0, int z0, int x1, int z1)
    {
        for (int gz = z0; gz <= z1; gz++)
        {
            for (int gx = x0; gx <= x1; gx++)
            {
                int p = gz * W + gx;
                Heights[p] = g.HeightOf(p);
                Limit[p] = g.AtLimit(p) ? 1 : 0;
                for (int c = 0; c < 4; c++)
                {
                    Mask[p * 4 + c] = (byte)Math.Clamp((int)MathF.Round(g.MaskOf(p, c) * 255f), 0, 255);
                }
            }
        }
    }
}
