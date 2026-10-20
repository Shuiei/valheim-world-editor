using System.Globalization;
using System.Numerics;
using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using TerrainEditor.Terrain;

/// <summary>
/// The snap check: the ground New world puts a location's SnapToGround objects on, against where the game put them
/// (a world a local server made with the AltDump plugin's -altdump-loc). Each location of the named kinds is taken
/// where the game placed it (its proxy's place and turn: a random turn cannot be foreseen), and each object near it of
/// a kind the location snaps is checked at its own place: the editor's ground there plus the offset against its
/// height (any of the offsets the kind puts that prefab at). Usage: snap, the world folder, then the kinds' name part.
/// </summary>
internal static class SnapCheck
{
    /// <summary>Prints the counts and the first differences; returns 0 when every snapped object is within a centimetre.</summary>
    /// <param name="args">snap, the world folder, then the kinds' name part.</param>
    /// <param name="world">The world.</param>
    internal static int Run(string[] args, WorldSave world)
    {
        if (args.Length < 3)
        {
            Console.Error.WriteLine("usage: WorldCheck snap <world> <name part>");
            return 2;
        }
        if (GameRules.Use(null) is null)
        {
            return 1;
        }
        var terrain = new ValheimGen.TerrainService(world, null);
        var objects = world.Objects.ToList();
        var kinds = LocationPrefabs.All.Where(p => p.Name.Contains(args[2], StringComparison.Ordinal)).ToDictionary(p => p.Hash);
        int locations = 0, checkedObjects = 0, off = 0;
        for (int i = 0; i < world.ObjectRefs.Count; i++)
        {
            if (world.ObjectRefs[i].Prefab != WorldSave.LocationProxyPrefab)
            {
                continue;
            }
            var proxy = ZdoData.Parse(world.ObjectBytes(i), world.ObjectRefs[i].File.WorldVersion);
            int? hash = proxy.IntList.Where(v => v.Key == WorldSave.LocationKey).Select(v => (int?)v.Value).FirstOrDefault();
            if (hash is not { } h || !kinds.TryGetValue(h, out var kind))
            {
                continue;
            }
            locations++;
            // A kind may put the same prefab at several offsets; its random turn hides which object is which.
            var offsets = kind.Contents.Where(c => c.Snap != null).GroupBy(c => StableHash.Of(c.Prefab)).ToDictionary(g => g.Key, g => g.Select(c => c.Snap!.Value).Distinct().ToList());
            var ground = new WorldCreator.SnapGround(terrain, h, proxy.Position, BlueprintFormats.FromEuler(proxy.Rotation));
            foreach (var (_, prefab, position, _, _) in objects.Where(o => offsets.ContainsKey(o.Prefab) && Vector2.Distance(new(o.Position.X, o.Position.Z), new(proxy.Position.X, proxy.Position.Z)) < 40f))
            {
                checkedObjects++;
                float under = ground.HeightAt(position), y = position.Y;
                float expected = offsets[prefab].Select(o => under + o).MinBy(e => MathF.Abs(y - e));
                if (MathF.Abs(position.Y - expected) > 0.01f && off++ < 25)
                {
                    Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"  {kind.Name}: {PrefabCatalog.NameOf(prefab)} at ({position.X:0.00}, {position.Z:0.00}) is at {position.Y:0.000}, the editor's ground gives {expected:0.000}"));
                }
            }
        }
        Console.WriteLine($"{locations} locations, {checkedObjects} snapped objects checked, {checkedObjects - off} within 1 cm of the editor's ground.");
        return locations > 0 && off == 0 ? 0 : 1;
    }
}
