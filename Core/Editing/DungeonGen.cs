using System.Numerics;
using static TerrainEditor.Editing.DungeonKit;

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
public static class DungeonGen
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
    public sealed record Item(string Prefab, Vector3 Position, Quaternion Rotation, IReadOnlyList<(string Section, string Key, string Value)>? Data = null);

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
    /// <param name="Notes">What was made and what could not be, for the user.</param>
    /// <param name="Name">The dungeon's.</param>
    /// <param name="Map">Its rooms (pieces).</param>
    /// <param name="Doors">Its doors and stairs between the rooms of Map (pieces).</param>
    /// <param name="Dungeon">
    /// The dungeon object's place and turn in the world when the rooms were made for one (Made.Rooms, see Make): Items,
    /// Arrival and Rooms are then in world metres, not from the dungeon's origin. Null otherwise.
    /// </param>
    public sealed record Result(List<Item> Items, Vector3 Arrival, Dungeons.Kind? Kind, List<Dungeons.Placed>? Rooms, List<string> Notes, string Name = "",
        IReadOnlyList<PlanRoom>? Map = null, IReadOnlyList<PlanDoor>? Doors = null, DungeonEntrances.Pose? Dungeon = null);

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

    /// <summary>An axis-aligned box of space (metres).</summary>
    /// <param name="Min">Its lowest corner.</param>
    /// <param name="Max">Its highest corner.</param>
    public readonly record struct Box(Vector3 Min, Vector3 Max)
    {
        /// <summary>Whether the two boxes share any space (touching faces do not count).</summary>
        public bool Overlaps(Box o) => Min.X < o.Max.X && o.Min.X < Max.X && Min.Y < o.Max.Y && o.Min.Y < Max.Y && Min.Z < o.Max.Z && o.Min.Z < Max.Z;
    }

    /// <summary>
    /// What is taken high up: objects above 3000 m (a dungeon object: the whole space its rooms keep),
    /// and the space over every location whose dungeon the game has not laid out yet.
    /// </summary>
    /// <param name="high">The objects high up (prefab hash, position).</param>
    /// <param name="locations">The world's locations (position, name hash); only those in DungeonLocations count.</param>
    public static List<Box> Taken(IEnumerable<(int Prefab, Vector3 Position)> high, IEnumerable<(Vector3 Position, int Location)> locations)
    {
        var boxes = new List<Box>();
        foreach (var (prefab, p) in high)
        {
            if (Dungeons.KindOf(prefab) is { } k)
            {
                var h = new Vector3(k.ZoneSize.X / 2 + 32, 60, k.ZoneSize.Z / 2 + 32);
                boxes.Add(new Box(p - h, p + h));
            }
            else
            {
                boxes.Add(new Box(p - Vector3.One, p + Vector3.One));
            }
        }
        foreach (var (p, location) in locations)
        {
            if (DungeonLocations.Contains(location))
            {
                boxes.Add(new Box(new Vector3(p.X - 96, p.Y + 4900, p.Z - 96), new Vector3(p.X + 96, p.Y + 5250, p.Z + 96)));
            }
        }
        return boxes;
    }

    /// <summary>
    /// Where a dungeon made at origin can go: there, or higher by 50 m steps until it is clear of what is
    /// taken (at most 80 steps, then it stays where the last step left it).
    /// </summary>
    public static Vector3 Clear(Vector3 origin, Result r, IReadOnlyList<Box> taken) =>
        Clear(origin, [.. r.Items.Select(i => i.Position), .. r.Rooms?.Select(p => p.Position) ?? []], r.Rooms != null ? 30 : 10, taken);

    /// <summary>
    /// Where a dungeon whose objects stand at these points (from its origin) can go: there, or higher by 50 m steps
    /// until it is clear of what is taken (at most 80 steps, then it stays where the last step left it).
    /// </summary>
    /// <param name="origin">Where it would go.</param>
    /// <param name="points">Its objects' places, from its origin.</param>
    /// <param name="pad">How much room to keep around them, metres.</param>
    /// <param name="taken">What is taken up there (Taken).</param>
    public static Vector3 Clear(Vector3 origin, IReadOnlyList<Vector3> points, float pad, IReadOnlyList<Box> taken)
    {
        if (points.Count == 0)
        {
            return origin;
        }
        var min = points.Aggregate(Vector3.Min) - new Vector3(pad, 6, pad);
        var max = points.Aggregate(Vector3.Max) + new Vector3(pad, 6, pad);
        for (int k = 0; k < 80 && taken.Any(t => t.Overlaps(new Box(origin + min, origin + max))); k++)
        {
            origin.Y += 50;
        }
        return origin;
    }

    /// <summary>The game's own dungeon of a biome (a DG_ prefab name), for Made.Rooms and for its entrance.</summary>
    public static string KindFor(string biome) => biome switch
    {
        "Meadows" or "Black Forest" => "DG_ForestCrypt",
        "Swamp" => "DG_SunkenCrypt",
        "Plains" => "DG_Hildir_PlainsFortress",
        "Mistlands" => "DG_DvergrTown",
        "Ashlands" => "DG_MorkHalla",
        _ => "DG_Cave",
    };

    /// <summary>A name for a dungeon of this style (null: the biome's); the same seed gives the same name.</summary>
    public static string NameFor(int seed, string? style = null, string? biome = null)
    {
        var r = new Random(seed * 7919 + 13);
        string[] starts = NameStarts(style ?? BiomeOf(biome).Style);
        return $"{starts[r.Next(starts.Length)]} of {NameEnds[r.Next(NameEnds.Length)]}";
    }

    // ---- From the game's rooms.
    /// <summary>
    /// Joins the game's rooms of the settings' kind (or the biome's) from an entrance, caps the open ends,
    /// fills them with the game's contents and doors, then thins or adds spawners and chests.
    /// </summary>
    private static Result MakeRooms(Settings s, int worldSeed, DungeonEntrances.Pose? dungeon)
    {
        var notes = new List<string>();
        static bool Joinable(Dungeons.Kind k) => Dungeons.RoomsFor(k).Any(r => !r.Entrance && !r.EndCap && !r.Divider && r.Openings.Length > 0);
        var cave = Dungeons.KindOf(Save.StableHash.Of("DG_Cave"));
        string wanted = s.Kind ?? KindFor(s.Biome);
        var kind = Dungeons.Kinds.FirstOrDefault(k => k.Name == wanted) ?? cave;
        // Camps and villages are laid out on the ground, not joined room to room: a cave instead.
        if (kind == null || !Joinable(kind))
        {
            if (cave == null || !Joinable(cave))
            {
                throw new InvalidDataException($"The game's data has no rooms to join for {wanted.Replace("DG_", "", StringComparison.Ordinal)}, and no cave to make instead "
                    + "(a game update may have changed its dungeons).");
            }
            // (No kind at all only when there is no cave either.)
            notes.Add($"{Describe(kind!)} has no rooms to join: a cave instead");
            kind = cave;
        }
        var rnd = new Random(s.Seed);
        var all = Dungeons.RoomsFor(kind).ToList();
        var entrances = all.Where(r => r.Entrance && !r.EndCap).ToList();
        var normal = all.Where(r => !r.Entrance && !r.EndCap && !r.Divider && r.Openings.Length > 0).ToList();
        var caps = all.Where(r => r.EndCap && r.Openings.Length == 1).ToList();
        var rooms = new List<Dungeons.Placed>();
        // From the dungeon object, as the game lays its rooms out (the first room's way in at it); without one, the
        // first room at the origin, facing +z.
        var origin = dungeon?.Position ?? Vector3.Zero;
        var first = entrances.Count > 0 ? entrances[rnd.Next(entrances.Count)] : normal[rnd.Next(normal.Count)];
        rooms.Add(dungeon is { } at ? Dungeons.PlaceStart(first, at.Position, at.Rotation) : new Dungeons.Placed(first.Hash, origin, Quaternion.Identity));
        int target = 2 + 6 * Math.Clamp(s.Size, 1, 4);
        for (int tries = 0; rooms.Count < target && tries < target * 40; tries++)
        {
            var ends = Dungeons.Openings(rooms, freeOnly: true);
            if (ends.Count == 0)
            {
                break;
            }
            var end = ends[rnd.Next(ends.Count)];
            var fits = normal.Where(r => Dungeons.Matching(r, end.Type).Any()).ToList();
            if (fits.Count == 0)
            {
                continue;
            }
            // Weighted as the game weighs its rooms (Room.m_weight).
#pragma warning disable S2245 // A dungeon must come out the same from its seed; nothing here is a secret.
            var room = Weighted.Pick(fits, r => r.Weight, rnd.NextDouble());
#pragma warning restore S2245
            var mine = Dungeons.Matching(room, end.Type).ToList();
            var p = Dungeons.Attach(room, mine[rnd.Next(mine.Count)], end.Position, end.Rotation);
            if (Dungeons.Overlaps(rooms, p).Count == 0 && Dungeons.Inside(kind, origin, p))
            {
                rooms.Add(p);
            }
        }
        // End caps on every open end; the entrance's way in too (no location's tunnel joins it here).
        var open = Dungeons.Openings(rooms, freeOnly: true).Concat(Dungeons.Openings(rooms, freeOnly: false).Where(e => e.Entrance)).ToList();
        int left = 0;
        foreach (var end in open)
        {
            var fit = caps.Where(c => c.Openings[0].Type == end.Type).ToList();
            if (fit.Count == 0)
            {
                left++;
                continue;
            }
            var cap = fit[rnd.Next(fit.Count)];
            rooms.Add(Dungeons.Attach(cap, cap.Openings[0], end.Position, end.Rotation));
        }
        if (left > 0)
        {
            notes.Add($"{left} open end(s) have no end cap of their kind");
        }
        // What the game puts in the rooms, then doors where rooms meet.
        var items = new List<Item>();
        foreach (var r in rooms)
        {
            foreach (var m in Dungeons.Contents(r, kind, origin, worldSeed))
            {
                items.Add(new Item(m.Prefab, m.Position, m.Rotation));
            }
        }
        foreach (var j in Dungeons.Joints(rooms))
        {
            if (Dungeons.RollDoor(kind, j, () => (float)rnd.NextDouble()) is string door)
            {
                items.Add(new Item(door, j.Position, j.Rotation));
            }
        }
        // Monsters: the rooms' own spawners, fewer or more, or others picked.
        var spawners = items.Where(i => i.Prefab.StartsWith("Spawner_", StringComparison.Ordinal)).ToList();
        foreach (var sp in spawners)
        {
            items.Remove(sp);
            int copies = (int)MathF.Floor(s.Monsters) + (rnd.NextDouble() < s.Monsters - MathF.Floor(s.Monsters) ? 1 : 0);
            for (int c = 0; c < copies; c++)
            {
                string prefab = s.Foes is { Count: > 0 } foes ? foes[rnd.Next(foes.Count)] : sp.Prefab;
                items.Add(sp with { Prefab = prefab, Position = sp.Position + (c == 0 ? Vector3.Zero : new Vector3((float)rnd.NextDouble() * 2 - 1, 0, (float)rnd.NextDouble() * 2 - 1)) });
            }
        }
        // Loot: the rooms' chests kept by chance (fewer), or more chests beside them.
        var chests = items.Where(i => i.Prefab.StartsWith("TreasureChest_", StringComparison.Ordinal)).ToList();
        foreach (var ch in chests)
        {
            if (s.Loot < 1 && rnd.NextDouble() > s.Loot)
            {
                items.Remove(ch);
            }
            else if (s.Loot > 1 && rnd.NextDouble() < s.Loot - 1)
            {
                items.Add(ch with { Position = ch.Position + Vector3.Transform(new Vector3(1.2f, 0, 0), ch.Rotation) });
            }
        }
        // Arriving in the entrance room, near its middle (the floor is found where it is placed).
        var arrival = rooms[0].Position;
        notes.Add($"{Describe(kind)}: {rooms.Count} rooms, {items.Count} objects");
        string name = string.IsNullOrWhiteSpace(s.Name) ? NameFor(s.Seed, null, s.Biome) : s.Name!;
        return new Result(items, arrival, kind, rooms, notes, name, Dungeon: dungeon);
    }

    /// <summary>
    /// A floor of its own for some rooms: in a stone or grausten dungeon, the grand rooms (halls, throne room, chapels,
    /// the arena, the treasury, the entrance) get black marble tiles half the time, and quarters, barracks, dens and
    /// studies wooden boards (with Decor 1 or more; the shell lays them).
    /// </summary>
    private static void Floors(DungeonPlan plan, Material mat, Settings s, Random rnd)
    {
        if (s.Decor < 1 || mat.Name is not ("Stone" or "Grausten"))
        {
            return;
        }
        foreach (var sp in plan.Spaces.Where(sp => sp.IsRoom))
        {
            bool grand = sp.Room is Room.Hall or Room.Throne or Room.Chapel or Room.Boss or Room.Treasury or Room.Entrance;
            bool lived = sp.Room is Room.Quarters or Room.Barracks or Room.Den or Room.Library;
            if (grand && rnd.NextDouble() < 0.5)
            {
                (sp.Floor, sp.FloorTop) = ("blackmarble_tile_floor_2x2", 0.19f);
            }
            else if (lived && rnd.NextDouble() < 0.5)
            {
                (sp.Floor, sp.FloorTop) = ("wood_floor", 0.1f);
            }
        }
    }

    /// <summary>
    /// A shaped room's floor as rectangles (metres): each row of its tiles in runs, rows with the same runs
    /// merged into one rectangle (an L is two, a cross three).
    /// </summary>
    private static List<(float X0, float Z0, float X1, float Z1)> Runs(DungeonPlan.Space sp)
    {
        var parts = new List<(float X0, float Z0, float X1, float Z1)>();
        foreach (var row in sp.Tiles.GroupBy(t => t.J).OrderBy(g => g.Key))
        {
            var xs = row.Select(t => t.I).Order().ToList();
            for (int a = 0; a < xs.Count;)
            {
                int b = a;
                while (b + 1 < xs.Count && xs[b + 1] == xs[b] + 1)
                {
                    b++;
                }
                float x0 = 2 * xs[a], x1 = 2 * xs[b] + 2, z0 = 2 * row.Key;
                int same = parts.FindIndex(p => p.X0 == x0 && p.X1 == x1 && p.Z1 == z0);
                if (same >= 0)
                {
                    parts[same] = (x0, parts[same].Z0, x1, z0 + 2);
                }
                else
                {
                    parts.Add((x0, z0, x1, z0 + 2));
                }
                a = b + 1;
            }
        }
        return parts;
    }

    /// <summary>A dungeon kind's name as shown, without the DG_ prefix.</summary>
    public static string Describe(Dungeons.Kind k) => k.Name.Replace("DG_", "", StringComparison.Ordinal);

    // ---- From building pieces.
    /// <summary>Plans the dungeon, builds its shell and dresses it, and draws its map.</summary>
    private static Result MakePieces(Settings s)
    {
        var rnd = new Random(s.Seed);
        var biome = BiomeOf(s.Biome);
        var style = StyleOf(s.Style ?? biome.Style);
        var mat = MaterialOf(s.Walls ?? biome.Material);
        string name = string.IsNullOrWhiteSpace(s.Name) ? NameFor(s.Seed, style.Name) : s.Name!;
        var plan = DungeonPlanner.Make(s, style, rnd);
        Floors(plan, mat, s, rnd);
        var o = new DungeonOut();
        DungeonShell.Build(plan, mat, o, s);
        DungeonDresser.Dress(plan, mat, biome, style, s, rnd, o, name);
        var e = plan.Entrance;
        // On the entrance's floor finish (marble tiles, boards), as what is set on its floor stands.
        var arrival = new Vector3(e.CenterX, new DungeonDresser.Frame(e, mat, plan.Levels[e.Level]).Top + DungeonDresser.Lift, 2 * e.Z0 + 1.7f);
        var map = plan.Spaces.Select(sp => new PlanRoom(sp.Name, sp.Level, DungeonPlan.FloorY(sp.Level), 2 * sp.X0, 2 * sp.Z0, 2 * (sp.X0 + sp.W), 2 * (sp.Z0 + sp.H),
            sp.Corridor, sp.Main, sp.KeyChest, sp.Shape == DungeonPlan.Shape.Rect ? null : Runs(sp),
            sp.Recesses.Count == 0 ? null : sp.Recesses.Select(n => (2f * n.Tiles.Min(t => t.I), 2f * n.Tiles.Min(t => t.J), 2f * (n.Tiles.Max(t => t.I) + 1), 2f * (n.Tiles.Max(t => t.J) + 1))).ToList())).ToList();
        var index = plan.Spaces.Select((sp, i) => (sp, i)).ToDictionary(p => p.sp, p => p.i);
        var doors = plan.Doors.Select(d => new PlanDoor(d.A.Level, d.Center.X, d.Center.Z, d.Kind.ToString(), index[d.A], index[d.B])).ToList();
        foreach (var foot in plan.Spaces.Where(sp => sp.Room == Room.StairBottom && sp.Parent != null))
        {
            doors.Add(new PlanDoor(foot.Level, foot.CenterX, foot.CenterZ, "Stairs", index[foot.Parent!], index[foot]));
        }
        var notes = new List<string>(plan.Notes);
        var key = plan.Spaces.FirstOrDefault(sp => sp.KeyChest);
        notes.Insert(0, $"{name}: {style.Name.ToLowerInvariant()} of {mat.Name.ToLowerInvariant()}, {plan.Levels.Count} level(s), {plan.Spaces.Count(sp => sp.IsRoom)} rooms, "
            + $"{o.Items.Count} objects" + (key != null ? $"; the key is in the {key.Name.ToLowerInvariant()} on level {key.Level + 1}" : ""));
        return new Result(o.Items, arrival, null, null, notes, name, map, doors);
    }
}
