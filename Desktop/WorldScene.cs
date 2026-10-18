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
public sealed partial class WorldScene
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
    /// <summary>World x of grid point 0 (the block's west edge: half a zone west of its first zone's middle).</summary>
    public float OriginX => X0 * 64f - 32f;
    /// <summary>World z of grid point 0 (the block's south edge).</summary>
    public float OriginZ => Z0 * 64f - 32f;
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
    /// <summary>
    /// How many times the things were read again from the world (after a save: EditSession.Reread). Their indices
    /// change then: what was worked out from them before (ids given to Claude) is out of date.
    /// </summary>
    public int Reads { get; set; }
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

    /// <summary>
    /// Where mods' extensions cut the ground, per grid point (1: not drawn, see GroundCuts), made with their shapes
    /// (GlView.RebuildShapes); empty until then.
    /// </summary>
    public float[] Cut { get; set; } = [];
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
    /// A thing as drawn: a part of a linked group (ObjectLinks) where its mod builds it (at its offset from its parent, the
    /// parent where its own parent puts it...), its root turned as stored (leaning too). Others are left as they are.
    /// What is stored stays (the game may keep a group's parts at its root).
    /// </summary>
    /// <param name="t">The thing, as read from the save or made for an added object.</param>
    /// <param name="links">The linked objects as they are now.</param>
    public static Thing Placed(Thing t, LinkIndex links) =>
        links.Pose(t.Id) is var (position, rotation) ? t with { Position = position, Rotation = rotation } : t;

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
}
