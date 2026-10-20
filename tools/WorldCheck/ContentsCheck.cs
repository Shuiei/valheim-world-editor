using System.Globalization;
using System.Numerics;
using TerrainEditor.App;
using TerrainEditor.Save;
using TerrainEditor.Terrain;

/// <summary>
/// The contents check: New world's build of the locations whose contents Expand World Data's file changes, against a
/// world where the game made them (a local server with the mod and the AltDump plugin's -altdump-loc): for each of
/// the named kind's locations, its objects by prefab and place on the ground, and their data (see Program.cs for the
/// usage). CONTENTS_DETAIL=n lists the first n locations' objects side by side.
/// </summary>
internal static class ContentsCheck
{
    /// <summary>The keys compared: the test's own (named vwe_…), the scale and the health.</summary>
    private static readonly HashSet<int> Keys = [.. new[] { "vwe_label", "vwe_count", "vwe_all", "vwe_bush", "vwe_own", "vwe_vines", "scale", "health" }.Select(StableHash.Of)];

    /// <summary>What the comparison counted.</summary>
    private sealed class Tally
    {
        /// <summary>Objects built, found, found with the game's values; locations left to the game; differences said.</summary>
        public int Expected, Placed, Valued, Left, Shown;

        /// <summary>The found objects' heights, the game's minus the editor's.</summary>
        public List<float> Heights { get; } = [];

        /// <summary>Says a difference, the first 25 only.</summary>
        public void Say(string what)
        {
            if (Shown++ < 25)
            {
                Console.WriteLine("  " + what);
            }
        }
    }

    /// <summary>Prints the counts and the first differences; returns 0 when every object and value matches.</summary>
    /// <param name="args">contents, the world folder, the BepInEx folder with the mod's files, then the kinds' name part.</param>
    /// <param name="world">The world.</param>
    internal static int Run(string[] args, WorldSave world)
    {
        if (GameRules.Use(null) is null)
        {
            return 1;
        }
        var found = ExpandWorld.Look(args[2], "check");
        world.Rules = ExpandWorld.Rules(new Dictionary<string, string>(), found, ExpandWorld.ReadData(found), world.StartingGlobalKeys);
        var terrain = new ValheimGen.TerrainService(world, null);
        var sites = WorldCreator.Sites(terrain, world.Seed, 160).Where(s => s.Name.Contains(args[3], StringComparison.Ordinal)).ToList();
        var objects = world.Objects.ToList();
        int detail = int.TryParse(Environment.GetEnvironmentVariable("CONTENTS_DETAIL"), out int n) ? n : 0;
        var tally = new Tally();
        foreach (var site in sites)
        {
            if (site.Objects is not { } made)
            {
                tally.Left++;
                continue;
            }
            var near = objects.Where(o => Vector3.Distance(o.Position, site.Position) < 25f).ToList();
            if (sites.IndexOf(site) < detail)
            {
                Detail(site, made, near);
            }
            foreach (var (at, zdo) in made)
            {
                tally.Expected++;
                Compare(world, site, near, at, zdo, tally);
            }
        }
        int grounds = Grounds(world, sites, tally);
        var h = tally.Heights;
        Console.WriteLine($"Heights of the matched objects, game minus editor: {(h.Count == 0 ? "none" : $"{h.Min():0.000} .. {h.Max():0.000}, {h.Count(d => MathF.Abs(d) > 0.01f)} off by more than 1 cm")}");
        Console.WriteLine($"{sites.Count} sites ({tally.Left} left to the game): {tally.Expected} objects built, {tally.Placed} where the game put one, {tally.Valued} of those with the game's values.");
        return tally.Placed == tally.Expected && tally.Valued == tally.Expected && tally.Left == 0 && grounds == 0 ? 0 : 1;
    }

    /// <summary>
    /// The zones whose ground the file levels or paints: the editor's against the game's terrain data of the zone,
    /// each height and paint cell; prints the count and returns how many zones differ.
    /// </summary>
    private static int Grounds(WorldSave world, List<WorldCreator.Site> sites, Tally tally)
    {
        int zones = 0, differ = 0;
        foreach (var site in sites.Where(s => s.Ground != null))
        {
            zones++;
            if (world.TerrainZones.FirstOrDefault(t => t.ZoneX == site.Zone.X && t.ZoneZ == site.Zone.Z) is not { } game)
            {
                differ++;
                tally.Say($"{site.Name} at {At(site.Position)}: the game's zone has no terrain data");
                continue;
            }
            int cells = Enumerable.Range(0, TerrainEditor.Editing.EditStore.Cells).Count(i => !SameCell(site.Ground!, game, i));
            if (cells > 0)
            {
                differ++;
                tally.Say($"{site.Name} at {At(site.Position)}: {cells} cells of its ground differ");
            }
        }
        Console.WriteLine($"Ground of {zones} zones levelled or painted: {zones - differ} as the game's.");
        return differ;
    }

    /// <summary>
    /// Whether a point of the editor's ground is the game's: its height change, and its paint (a 64 × 64 paint grid
    /// read as the game reads it, the last row and column repeated).
    /// </summary>
    private static bool SameCell(TerrainEditor.Editing.ZoneEdit mine, TerrainZone game, int i)
    {
        int w = game.PaintWidth, row = Math.Min(i / 65, w - 1), col = Math.Min(i % 65, w - 1), p = row * w + col;
        bool height = mine.Modified[i] == game.ModifiedHeight[i] && (!mine.Modified[i] || MathF.Abs(mine.Level[i] - game.LevelDelta[i]) < 1e-4f);
        var paint = new Vector4(mine.Paint[i * 4], mine.Paint[i * 4 + 1], mine.Paint[i * 4 + 2], mine.Paint[i * 4 + 3]);
        return height && mine.PaintModified[i] == game.ModifiedPaint[p] && (!mine.PaintModified[i] || Vector4.Distance(paint, game.Paint[p]) < 1e-4f);
    }

    /// <summary>One built object against the world: the same prefab within 5 cm on the ground, then its values.</summary>
    private static void Compare(WorldSave world, WorldCreator.Site site, List<(int Id, int Prefab, Vector3 Position, Vector3 Rotation, Vector3 Scale)> near, Vector3 at, byte[] zdo, Tally tally)
    {
        int prefab = BitConverter.ToInt32(zdo, 14);
        if (prefab == WorldSave.LocationProxyPrefab)
        {
            // The world keeps proxies apart from its objects (a runestone's shows as the stone).
            if (ZdoData.Parse(zdo).IntList.Where(v => v.Key == WorldSave.LocationKey).Select(v => (int?)v.Value).FirstOrDefault() is not { } location)
            {
                BlueprintProxy(world, site, at, zdo, tally);
                return;
            }
            var proxy = world.Placed.FirstOrDefault(p => p.Location == location && Flat(p.Position, at) < 0.05f);
            if (proxy == null)
            {
                tally.Say($"{site.Name} at ({site.Position.X:0.0}, {site.Position.Z:0.0}): no proxy");
                return;
            }
            tally.Placed++;
            tally.Valued++;
            tally.Heights.Add(proxy.Position.Y - at.Y);
            return;
        }
        // Of those on the spot, the nearest in height (stacked pieces share a spot).
        int match = near.Select((o, i) => (o, i)).Where(c => c.o.Prefab == prefab && Flat(c.o.Position, at) < 0.05f)
            .OrderBy(c => MathF.Abs(c.o.Position.Y - at.Y)).Select(c => c.i).DefaultIfEmpty(-1).First();
        if (match < 0)
        {
            var nearest = near.Where(o => o.Prefab == prefab).OrderBy(o => Flat(o.Position, at)).Select(o => o.Position).Cast<Vector3?>().FirstOrDefault();
            tally.Say($"{site.Name} at ({site.Position.X:0.0}, {site.Position.Z:0.0}): no {PrefabCatalog.NameOf(prefab)} at {At(at)}; nearest {(nearest is { } p ? At(p) : "none")}");
            return;
        }
        var (id, _, position, _, _) = near[match];
        near.RemoveAt(match);
        tally.Placed++;
        tally.Heights.Add(position.Y - at.Y);
        string mine = Values(ZdoData.Parse(zdo), Keys), game = Values(ZdoData.Parse(world.ObjectBytes(id)), Keys);
        if (mine == game)
        {
            tally.Valued++;
        }
        else
        {
            tally.Say($"{site.Name}: {PrefabCatalog.NameOf(prefab)} values differ\n    editor: {mine}\n    game:   {game}");
        }
    }

    /// <summary>
    /// A blueprint location's proxy, which names no location (the world keeps it as an object): a proxy of the world
    /// within 5 cm on the ground, with the same "locationreference".
    /// </summary>
    private static void BlueprintProxy(WorldSave world, WorldCreator.Site site, Vector3 at, byte[] zdo, Tally tally)
    {
        int reference = StableHash.Of("locationreference");
        int? mine = ZdoData.Parse(zdo).IntList.Where(v => v.Key == reference).Select(v => (int?)v.Value).FirstOrDefault();
        int id = world.ObjectRefs.FindIndex(o => o.Prefab == WorldSave.LocationProxyPrefab && Flat(o.Position, at) < 0.05f);
        if (id < 0)
        {
            tally.Say($"{site.Name} at ({site.Position.X:0.0}, {site.Position.Z:0.0}): no proxy");
            return;
        }
        tally.Placed++;
        tally.Heights.Add(world.ObjectRefs[id].Position.Y - at.Y);
        int? game = ZdoData.Parse(world.ObjectBytes(id)).IntList.Where(v => v.Key == reference).Select(v => (int?)v.Value).FirstOrDefault();
        if (mine == game)
        {
            tally.Valued++;
        }
        else
        {
            tally.Say($"{site.Name}: its proxy's reference is {game}, the editor's {mine}");
        }
    }

    /// <summary>A location's built objects and the world's objects within 10 m, places from its spot.</summary>
    private static void Detail(WorldCreator.Site site, List<(Vector3 At, byte[] Zdo)> made, List<(int Id, int Prefab, Vector3 Position, Vector3 Rotation, Vector3 Scale)> near)
    {
        Console.WriteLine($"== {site.Name} at {At(site.Position)}");
        foreach (var (at, zdo) in made)
        {
            Console.WriteLine($"  editor {PrefabCatalog.NameOf(BitConverter.ToInt32(zdo, 14))} {At(at - site.Position)}");
        }
        foreach (var (_, prefab, position, _, _) in near.Where(o => Vector3.Distance(o.Position, site.Position) < 10f))
        {
            Console.WriteLine($"  game   {PrefabCatalog.NameOf(prefab) ?? prefab.ToString(CultureInfo.InvariantCulture)} {At(position - site.Position)}");
        }
    }

    /// <summary>The distance between two points on the ground (x and z).</summary>
    private static float Flat(Vector3 a, Vector3 b) => MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Z - b.Z) * (a.Z - b.Z));

    /// <summary>A point as text.</summary>
    private static string At(Vector3 p) => string.Create(CultureInfo.InvariantCulture, $"({p.X:0.00}, {p.Y:0.00}, {p.Z:0.00})");

    /// <summary>The values of these keys, as text.</summary>
    internal static string Values(ZdoData z, HashSet<int> keys) => string.Join("; ", z.IntList.Where(v => keys.Contains(v.Key)).Select(v => $"i{v.Key}={v.Value}")
        .Concat(z.FloatList.Where(v => keys.Contains(v.Key)).Select(v => $"f{v.Key}={v.Value.ToString(CultureInfo.InvariantCulture)}"))
        .Concat(z.StringList.Where(v => keys.Contains(v.Key)).Select(v => $"s{v.Key}={v.Value}"))
        .Concat(z.Vec3List.Where(v => keys.Contains(v.Key)).Select(v => $"v{v.Key}={v.Value.X:0.###},{v.Value.Y:0.###},{v.Value.Z:0.###}"))
        .Order(StringComparer.Ordinal));
}
