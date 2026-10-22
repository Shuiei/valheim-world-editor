using System.Numerics;
using TerrainEditor.App;

namespace TerrainEditor.Editing;

/// <summary>
/// Dungeons made from settings and a seed (the same settings make the same dungeon; from the game's rooms,
/// the same world too, for kinds whose room contents add the world's seed), in two ways:
/// <list type="bullet">
/// <item>from the game's own rooms (Rooms): an entrance, then rooms of the dungeon's kind joined at their
///   openings while they fit (the game's rules: Dungeons.Attach, no overlap, inside its space), then the
///   game's end cap on every open end; each room with what the game puts in it, and doors where rooms
///   meet. The game builds it from its room list, like any dungeon.</item>
/// <item>from building pieces (Pieces): a style (crypt, catacombs, temple, fortress, prison, Dvergr hold,
///   goblin warren, ruins) gives the rooms and how they are laid out (DungeonPlanner), the walls are the
///   biome's or those picked (DungeonShell), and each room is furnished for what it is, lit, guarded and
///   stocked (DungeonDresser): an entrance with the dungeon's name, halls, side rooms, loops, stairs
///   down, an antechamber, the boss's arena behind a gate whose key lies in a chest somewhere, the
///   treasury beyond, a hidden cache or two. Floors and roofs are black marble slabs, the one piece the
///   game never checks for support; everything else stands on them, so it holds anywhere, 5000 m up as
///   on the ground.</item>
/// </list>
/// Positions are metres from the dungeon's origin (its entrance; y up from the first floor).
/// </summary>
public static partial class DungeonGen
{
    /// <summary>How a dungeon is made: from the game's own rooms (Rooms), or from building pieces (Pieces).</summary>
    public enum Made
    {
        /// <summary>From the game's own rooms.</summary>
        Rooms,
        /// <summary>From building pieces.</summary>
        Pieces,
    }

    /// <summary>
    /// What to make; the same settings make the same dungeon (from the game's rooms, in the same world: some
    /// kinds add the world's seed to their room contents, <c>Kind.BaseSeed</c>).
    /// </summary>
    /// <param name="Made">From the game's rooms or from building pieces.</param>
    /// <param name="Biome">
    /// The biome whose style, walls, foes and boss are used where none is picked (Made.Rooms: whose game dungeon).
    /// </param>
    /// <param name="Style">A DungeonKit style (none: the biome's).</param>
    /// <param name="Walls">A material (none: the biome's).</param>
    /// <param name="Size">1 small to 4 huge.</param>
    /// <param name="Levels">1 to <see cref="MaxLevels"/>.</param>
    /// <param name="Monsters">0 none, 1 usual, up to 3.</param>
    /// <param name="Respawn">Spawners (they come back) or the creatures once.</param>
    /// <param name="Foes">Spawners or creatures to use (none: the biome's).</param>
    /// <param name="Boss">Its spawner or creature ("": none, null: the biome's).</param>
    /// <param name="Loot">0 none to 2 rich.</param>
    /// <param name="Light">0 dark, 1 dim, 2 bright.</param>
    /// <param name="Decor">0 bare, 1 usual, 2 rich.</param>
    /// <param name="Decay">0 kept to 1 ruined (null: the style's).</param>
    /// <param name="Loops">How many ways round (0 none, 1 usual, 2 many).</param>
    /// <param name="BossKey">The arena locked, its key in a chest.</param>
    /// <param name="Secrets">Hidden caches.</param>
    /// <param name="Seed">The dungeon's own seed: everything random about it comes from it.</param>
    /// <param name="Name">On the entrance's sign (null: one is made up).</param>
    /// <param name="Kind">The game dungeon kind to join rooms of, by name (Made.Rooms; null: the biome's).</param>
    public sealed record Settings(Made Made = Made.Pieces, string Biome = "Black Forest", string? Style = null, string? Walls = null, int Size = 2, int Levels = 2,
        float Monsters = 1, bool Respawn = true, IReadOnlyList<string>? Foes = null, string? Boss = null, float Loot = 1, int Light = 1, int Decor = 1,
        float? Decay = null, float Loops = 1, bool BossKey = true, bool Secrets = true, int Seed = 1, string? Name = null, string? Kind = null);

    /// <summary>An object to place.</summary>
    /// <param name="Prefab">The object's prefab name.</param>
    /// <param name="Position">Metres from the dungeon's origin.</param>
    /// <param name="Rotation">Its rotation.</param>
    /// <param name="Data">Values of its object data (section, key, value as text; see ZdoData.Set).</param>
    /// <param name="Fields">Values of its object data by key hash (Expand World Data's files give these); after <paramref name="Data"/>.</param>
    public sealed record Item(string Prefab, Vector3 Position, Quaternion Rotation, IReadOnlyList<(string Section, string Key, string Value)>? Data = null,
        IReadOnlyList<ValheimGen.ContentField>? Fields = null)
    {
        /// <summary>Its object data, <see cref="Data"/> then <see cref="Fields"/>.</summary>
        public IEnumerable<ObjectField> Values() =>
            (Data ?? []).Select(d => new ObjectField(d.Section, Save.StableHash.Of(d.Key), d.Value)).Concat((Fields ?? []).Select(f => new ObjectField(f.Section, f.Key, f.Value)));
    }

    /// <summary>A room of the plan (metres, its level's floor at Y), for a map.</summary>
    /// <param name="Name">What the room is, as shown.</param>
    /// <param name="Level">Its level, 0 the first.</param>
    /// <param name="Y">Its level's floor (metres, below 0 for the levels under the first).</param>
    /// <param name="X0">Lowest x (metres).</param>
    /// <param name="Z0">Lowest z (metres).</param>
    /// <param name="X1">Highest x (metres).</param>
    /// <param name="Z1">Highest z (metres).</param>
    /// <param name="Corridor">A corridor rather than a room.</param>
    /// <param name="Main">On the main way through.</param>
    /// <param name="Key">The room whose chest holds the boss gate's key.</param>
    /// <param name="Parts">
    /// The rectangles (metres) its floor is made of when it is not its whole box (a corner cut away, notched corners,
    /// an apse); null for a whole box.
    /// </param>
    /// <param name="Nooks">Its recesses (metres): the alcoves off a room, the niches off a corridor; among its parts too.</param>
    public sealed record PlanRoom(string Name, int Level, float Y, float X0, float Z0, float X1, float Z1, bool Corridor, bool Main, bool Key,
        IReadOnlyList<(float X0, float Z0, float X1, float Z1)>? Parts = null, IReadOnlyList<(float X0, float Z0, float X1, float Z1)>? Nooks = null)
    {
        /// <summary>Its floor as rectangles: its parts, or its whole box.</summary>
        public IReadOnlyList<(float X0, float Z0, float X1, float Z1)> Floor => Parts ?? [(X0, Z0, X1, Z1)];
    }

    /// <summary>A way between two rooms of the map: a doorway, or Kind "Stairs".</summary>
    /// <param name="Level">The level it is on (for stairs, the foot's).</param>
    /// <param name="X">Its middle (metres).</param>
    /// <param name="Z">Its middle (metres).</param>
    /// <param name="Kind">The door's kind, or "Stairs".</param>
    /// <param name="A">Index of one room in the map (-1: none).</param>
    /// <param name="B">Index of the other room in the map (-1: none).</param>
    public sealed record PlanDoor(int Level, float X, float Z, string Kind, int A = -1, int B = -1);

    /// <summary>A made dungeon.</summary>
    /// <param name="Items">The objects to place (pieces, spawners, chests, doors, room contents).</param>
    /// <param name="Arrival">Where to stand on arriving (a portal there faces the way in).</param>
    /// <param name="Kind">The game dungeon kind the rooms belong to (Made.Rooms; null for pieces).</param>
    /// <param name="Rooms">The game rooms' list (Made.Rooms) for a dungeon object of Kind at the origin.</param>
    /// <param name="Notes">What was made and what could not be, for the user (and in English for Claude and the blueprint).</param>
    /// <param name="Name">The dungeon's.</param>
    /// <param name="Map">Its rooms (pieces).</param>
    /// <param name="Doors">Its doors and stairs between the rooms of Map (pieces).</param>
    /// <param name="Dungeon">
    /// The dungeon object's place and turn in the world when the rooms were made for one (Made.Rooms, see Make): Items,
    /// Arrival and Rooms are then in world metres, not from the dungeon's origin. Null otherwise.
    /// </param>
    public sealed record Result(List<Item> Items, Vector3 Arrival, Dungeons.Kind? Kind, List<Dungeons.Placed>? Rooms, List<Said> Notes, string Name = "",
        IReadOnlyList<PlanRoom>? Map = null, IReadOnlyList<PlanDoor>? Doors = null, DungeonEntrances.Pose? Dungeon = null)
    {
        /// <summary>The notes as one line: what was made first, then each thing that could not be, apart.</summary>
        public Said Summary => Said.Join("; ", Notes);
    }

    /// <summary>The most levels a dungeon of building pieces can have.</summary>
    public const int MaxLevels = 16;

    /// <summary>Makes a dungeon from <paramref name="s"/>.</summary>
    /// <param name="s">What to make.</param>
    /// <param name="worldSeed">The world's seed, for the game's own room contents (Made.Rooms only).</param>
    /// <param name="dungeon">
    /// Made.Rooms: the dungeon object's place and turn in the world (an entrance's, see DungeonEntrances): the rooms are
    /// then laid out from there as the game lays them out from its dungeon object (the first room's way in at it, the
    /// rooms kept in its zone's box), in world metres. Null: from the origin, facing +z.
    /// </param>
    /// <exception cref="InvalidDataException">The game's rooms are asked for and its data has none to join (nor a cave).</exception>
    public static Result Make(Settings s, int worldSeed = 0, DungeonEntrances.Pose? dungeon = null) => s.Made == Made.Rooms ? MakeRooms(s, worldSeed, dungeon) : MakePieces(s);

    /// <summary>
    /// Locations with a dungeon (name hashes), which the game lays out 5000 m above them when a player first
    /// comes near: nothing of it is in the save until then.
    /// </summary>
    public static readonly HashSet<int> DungeonLocations = [.. DungeonEntrances.Locations.Select(l => Save.StableHash.Of(l.Name))];
}
