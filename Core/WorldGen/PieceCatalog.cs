using System.Text.Json;
using TerrainEditor.Save;

namespace TerrainEditor.Terrain;

/// <summary>
/// Build pieces (prefabs with a Piece component), read from the player's own Valheim (GameData; the
/// tests use tests/fixtures/pieces.json): category and footprint (bounds of the non-trigger box
/// colliders in the piece's own frame), so player-built structures can be drawn on the map, and snap
/// points (where the hammer snaps pieces together).
/// </summary>
public static class PieceCatalog
{
    /// <summary>
    /// One piece's catalogue entry. The bounds are those of its non-trigger box colliders in the piece's own frame,
    /// in metres (a 1 m marker for pieces without box colliders).
    /// </summary>
    /// <param name="Name">The prefab name.</param>
    /// <param name="Index">Its place in <see cref="Names"/>.</param>
    /// <param name="Category">The game's build menu category number.</param>
    /// <param name="MinX">Footprint, lowest x.</param>
    /// <param name="MaxX">Footprint, highest x.</param>
    /// <param name="MinZ">Footprint, lowest z.</param>
    /// <param name="MaxZ">Footprint, highest z.</param>
    /// <param name="MinY">Bottom, relative to the piece's origin (y).</param>
    /// <param name="MaxY">Top, relative to the piece's origin (y).</param>
    public sealed record Info(string Name, int Index, int Category, float MinX, float MaxX, float MinZ, float MaxZ, float MinY, float MaxY)
    {
        /// <summary>
        /// Snap points in the piece's own frame (x, y, z each); empty when it has none.
        /// </summary>
        public float[][] Snaps { get; init; } = [];

        /// <summary>
        /// The build tool whose menu has the piece (hammer, hoe, cultivator, feaster: the serving tray);
        /// null for pieces players cannot build (parts of ruins and dungeons).
        /// </summary>
        public string? Tool { get; init; }

        /// <summary>
        /// Whether players build it: in a build tool's menu (the game's or a mod's), not the hoe's (ground edits).
        /// </summary>
        public bool IsBuildPiece => Tool is not null and not "hoe";
    }

    /// <summary>
    /// None until Use: no world or Workshop opens before then (GameData).
    /// </summary>
    private static volatile Data _catalogue = new([], [], []);

    /// <summary>
    /// What Use read, swapped in whole so readers never see half of it.
    /// </summary>
    /// <param name="ByHash">Entries by prefab name hash (as the save keeps prefabs).</param>
    /// <param name="Names">Prefab names in catalogue (ordinal) order.</param>
    /// <param name="Known">The same names, for lookups by name.</param>
    private sealed record Data(Dictionary<int, Info> ByHash, string[] Names, HashSet<string> Known);

    /// <summary>
    /// Every piece with snap points.
    /// </summary>
    public static IEnumerable<Info> WithSnaps => _catalogue.ByHash.Values.Where(i => i.Snaps.Length > 0);

    /// <summary>
    /// Prefab names in catalogue order; Info.Index points into this list.
    /// </summary>
    public static string[] Names => _catalogue.Names;

    /// <summary>
    /// How many pieces the catalogue has.
    /// </summary>
    public static int Count => _catalogue.ByHash.Count;

    /// <summary>
    /// A piece by prefab name hash; null when the prefab is not a piece.
    /// </summary>
    public static Info? Get(int prefab) => _catalogue.ByHash.TryGetValue(prefab, out var info) ? info : null;

    /// <summary>
    /// Whether a prefab (by name) is a piece.
    /// </summary>
    public static bool Has(string name) => _catalogue.Known.Contains(name);

    /// <summary>
    /// Floats per piece in <see cref="Encode"/>'s output: x, y, z, rotation Y (degrees), footprint minX, maxX, minZ,
    /// maxZ, top (y), category, bottom (y), prefab (index into Names), object id.
    /// </summary>
    public const int Stride = 13;

    /// <summary>
    /// The world's player-built pieces packed for the map, <see cref="Stride"/> floats each, leaving out deleted
    /// objects and prefabs that are not in the catalogue.
    /// </summary>
    /// <param name="world">The world; its piece list is copied under its lock.</param>
    /// <param name="deleted">Object ids to leave out.</param>
    public static float[] Encode(WorldSave world, ICollection<int> deleted)
    {
        List<float> data = new(world.Pieces.Count * Stride);
        List<(int Id, int Prefab, System.Numerics.Vector3 Position, float RotationY)> pieces;
        lock (world.Sync)
        {
            pieces = [.. world.Pieces];
        }
        foreach (var (id, prefab, pos, rotY) in pieces)
        {
            if (deleted.Contains(id))
            {
                continue;
            }
            Info? info = Get(prefab);
            if (info == null)
            {
                continue;
            }
            data.AddRange([pos.X, pos.Y, pos.Z, rotY, info.MinX, info.MaxX, info.MinZ, info.MaxZ, pos.Y + info.MaxY, info.Category, pos.Y + info.MinY, info.Index, id]);
        }
        return [.. data];
    }

    /// <summary>
    /// The ruins' pieces (building pieces without a builder: generated with the world) packed as
    /// <see cref="Encode"/> packs the player-built ones, leaving out deleted objects; and which of them are joined to
    /// a player-built piece (<see cref="App.JoinedRuins"/>).
    /// </summary>
    /// <param name="world">The world; its object and piece lists are copied under its lock.</param>
    /// <param name="deleted">Object ids to leave out.</param>
    public static (float[] Data, bool[] Joined) EncodeRuins(WorldSave world, ICollection<int> deleted)
    {
        List<(int Id, int Prefab, System.Numerics.Vector3 Position, System.Numerics.Vector3 Rotation)> ruins;
        HashSet<int> joined;
        lock (world.Sync)
        {
            ruins = [.. world.Objects.Where(o => !deleted.Contains(o.Id) && Get(o.Prefab) != null).Select(o => (o.Id, o.Prefab, o.Position, o.Rotation))];
            joined = App.JoinedRuins.InWorld(world, deleted);
        }
        List<float> data = new(ruins.Count * Stride);
        foreach (var (id, prefab, pos, rot) in ruins)
        {
            Info info = Get(prefab)!;
            data.AddRange([pos.X, pos.Y, pos.Z, rot.Y, info.MinX, info.MaxX, info.MinZ, info.MaxZ, pos.Y + info.MaxY, info.Category, pos.Y + info.MinY, info.Index, id]);
        }
        return ([.. data], [.. ruins.Select(r => joined.Contains(r.Id))]);
    }

    /// <summary>
    /// Takes the pieces by name, as GameCatalogues' PieceScan writes them.
    /// </summary>
    public static void Use(string json)
    {
        using var doc = JsonDocument.Parse(json);
        Dictionary<int, Info> result = [];
        List<string> names = [];
        foreach (JsonProperty p in doc.RootElement.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal))
        {
            int cat = p.Value.GetProperty("cat").GetInt32();
            // Pieces without box colliders get a 1 m marker.
            float[] box = p.Value.TryGetProperty("box", out JsonElement b) ? [.. b.EnumerateArray().Select(e => e.GetSingle())] : [-0.5f, 0.5f, -0.5f, 0.5f, 0f, 1f];
            float[][] snaps = p.Value.TryGetProperty("snap", out JsonElement sp) ? [.. sp.EnumerateArray().Select(v => v.EnumerateArray().Select(e => e.GetSingle()).ToArray())] : [];
            string? tool = p.Value.TryGetProperty("tool", out JsonElement t) ? t.GetString() : null;
            result[StableHash.Of(p.Name)] = new Info(p.Name, names.Count, cat, box[0], box[1], box[2], box[3], box[4], box[5]) { Snaps = snaps, Tool = tool };
            names.Add(p.Name);
        }
        _catalogue = new Data(result, [.. names], names.ToHashSet(StringComparer.Ordinal));
    }
}
