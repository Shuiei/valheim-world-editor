using System.Numerics;
using System.Text.Json;
using TerrainEditor.Save;

namespace TerrainEditor.Editing;

/// <summary>
/// The game's location prefabs (the start temple, a boss's altar, ruins, a camp, a dungeon's entrance...) as GameData's
/// dungeon scan describes them: what the game makes of one when it spawns it in a zone (ZoneSystem.SpawnLocation),
/// for the zones a new world generates itself (WorldCreator). The save keeps a location proxy for the parts the game
/// makes again from the prefab at every load, and the prefab's networked objects (its ZNetView children: stones,
/// chests, spawners) as objects of their own, those its random parts keep.
/// </summary>
public static class LocationPrefabs
{
    /// <summary>A location prefab.</summary>
    /// <param name="Name">Its prefab name (the location kind's, LocationRule.prefab).</param>
    /// <param name="Hash">The stable hash of its name: the proxy's location, the zone file's entry.</param>
    /// <param name="ClearArea">The game clears the vegetation around it (Location.m_clearArea), as far as its kind's exterior radius.</param>
    /// <param name="RandomDamage">Its pieces are spawned worn at random (Location.m_applyRandomDamage).</param>
    /// <param name="Interior">
    /// It has an interior the game lays out itself when it spawns it (Location.m_hasInterior: a dungeon's entrance).
    /// </param>
    /// <param name="Contents">The networked objects it makes, in its own frame.</param>
    /// <param name="Spawns">Its RandomSpawn parts, rolled in order.</param>
    /// <param name="Picks">Its RandomObject parts, rolled after the spawns.</param>
    /// <param name="Nodes">Each random part's parent node (-1: the prefab itself).</param>
    /// <param name="CustomInterior">
    /// The game moves its interior (everything under its interior transform: its dungeon and the door out of it) to
    /// the middle of the zone, turned square to the world (Location.m_useCustomInteriorTransform).
    /// </param>
    /// <param name="Teleports">
    /// Its teleport doors, in its own frame: the one at the ground leads into the interior, the one 5000 m up leads out.
    /// Each faces the way a player stands on arriving through it.
    /// </param>
    /// <param name="InteriorAt">Where its interior transform stands in its own frame; null when it has none.</param>
    public sealed record Prefab(string Name, int Hash, bool ClearArea, bool RandomDamage, bool Interior,
        Dungeons.Content[] Contents, Dungeons.Spawn[] Spawns, Dungeons.Pick[] Picks, int[] Nodes,
        bool CustomInterior = false, IReadOnlyList<Frame>? Teleports = null, Frame? InteriorAt = null)
    {
        /// <summary>Its teleport doors (none when the game's files do not describe them).</summary>
        public IReadOnlyList<Frame> Doors => Teleports ?? [];

        /// <summary>The door out of its interior (the teleport 5000 m up in the prefab), or null when it has none.</summary>
        public Frame? Exit => Doors.FirstOrDefault(t => t.Position.Y > 1000f);

        /// <summary>The door into its interior (the teleport at the ground), or null when it has none.</summary>
        public Frame? Entrance => Doors.FirstOrDefault(t => t.Position.Y <= 1000f);
    }

    /// <summary>A place and a turn in a location's own frame: a teleport door (which faces the way a player arriving
    /// through it stands, one metre ahead), or its interior transform.</summary>
    /// <param name="Position">Where it stands.</param>
    /// <param name="Rotation">Which way it is turned.</param>
    public sealed record Frame(Vector3 Position, Quaternion Rotation);

    /// <summary>The prefabs read, by name. None until Use: no world is made before then (GameData).</summary>
    private static volatile Dictionary<string, Prefab> _all = new(StringComparer.Ordinal);

    /// <summary>
    /// Takes the locations of the "dungeon-rooms" JSON as GameCatalogues' DungeonScan writes it (none when it has no
    /// locations part), in place of any before.
    /// </summary>
    public static void Use(string json) => _all = Load(json);

    /// <summary>The prefab of this location kind, or null when the game's files do not describe it.</summary>
    public static Prefab? Get(string name) => _all.GetValueOrDefault(name);

    /// <summary>Every location prefab, by name.</summary>
    public static IEnumerable<Prefab> All => _all.Values.OrderBy(p => p.Name, StringComparer.Ordinal);

    /// <summary>
    /// The seed the game spawns a zone's location with (ZoneSystem.SpawnZone): the world's seed and the zone's place.
    /// </summary>
    public static int ZoneSeed(int worldSeed, int zx, int zz) => unchecked(worldSeed + zx * 4271 + zz * 9187);

    /// <summary>
    /// What the game makes with a location it spawns at this place (ZoneSystem.SpawnLocation, SpawnMode Full): its
    /// networked objects, those its random parts keep, rolled the game's way from the zone's seed (Random.InitState,
    /// then each RandomSpawn's chance, then each RandomObject's pick, in order; a part's elevation is the location's).
    /// The shared Unity random state is put back after.
    /// </summary>
    /// <param name="prefab">The location's prefab.</param>
    /// <param name="position">Where it stands, world metres.</param>
    /// <param name="rotation">Its turn.</param>
    /// <param name="seed">The zone's seed (ZoneSeed).</param>
    public static List<Dungeons.Made> Made(Prefab prefab, Vector3 position, Quaternion rotation, int seed) =>
        Made(prefab, position, rotation, seed, out _);

    /// <summary>
    /// <see cref="Made(Prefab, Vector3, Quaternion, int)"/>, with the random state its rolls leave (what the game draws
    /// next from as it makes the objects: Expand World Data's swaps and data).
    /// </summary>
    /// <param name="prefab">The location's prefab.</param>
    /// <param name="position">Where it stands, world metres.</param>
    /// <param name="rotation">Its turn.</param>
    /// <param name="seed">The zone's seed (ZoneSeed).</param>
    /// <param name="rolled">The random state after the rolls.</param>
    public static List<Dungeons.Made> Made(Prefab prefab, Vector3 position, Quaternion rotation, int seed, out ValheimGen.UnityEngine.Random.RandomState rolled) =>
        Dungeons.Replay(prefab.Contents, prefab.Spawns, prefab.Picks, prefab.Nodes, -1, position.Y, seed, position, rotation, out rolled);

    /// <summary>Reads the "locations" of the scan's JSON (by prefab name); none when the JSON has no such part.</summary>
    private static Dictionary<string, Prefab> Load(string json)
    {
        var all = new Dictionary<string, Prefab>(StringComparer.Ordinal);
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("locations", out var locations))
        {
            return all;
        }
        foreach (JsonProperty p in locations.EnumerateObject())
        {
            JsonElement l = p.Value;
            bool B(string k) => l.GetProperty(k).GetInt32() != 0;
            var (contents, spawns, picks, nodes) = Dungeons.Parts(l);
            // Teleports and the interior's place: of scans since v1.39; older copies have neither.
            var teleports = l.TryGetProperty("teleports", out var ts) && ts.ValueKind == JsonValueKind.Array ? ts.EnumerateArray().Select(t => new Frame(Dungeons.V(t, 0), Dungeons.Q(t, 3))).ToList() : [];
            Frame? interiorAt = l.TryGetProperty("interiorAt", out var at) && at.ValueKind == JsonValueKind.Array ? new Frame(Dungeons.V(at, 0), Dungeons.Q(at, 3)) : null;
            bool custom = l.TryGetProperty("customInterior", out var c) && c.GetInt32() != 0;
            all[p.Name] = new Prefab(p.Name, StableHash.Of(p.Name), B("clearArea"), B("randomDamage"), B("interior"), contents, spawns, picks, nodes, custom, teleports, interiorAt);
        }
        return all;
    }
}
