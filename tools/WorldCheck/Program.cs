using TerrainEditor.App;
using TerrainEditor.Save;

// Developer checks of the world generator and the save writer against the game (moved here from the
// old web app's command line; players never need them). Each check is a class in its own file:
//   verify <dump> [seed]          the generator against a dump recorded in the game (DumpVerifier)
//   verify-ingame <world> <file>  the editor's ground against heightmaps recorded by TerrainCheck (IngameVerifyCheck)
//   selftest-save <world copy>    edits, saves and reloads a COPY of a world (under /tmp only) (SaveSelfTestCheck)
//   inspect <world>               the chunk mapping and the layout of terrain objects (InspectCheck)
//   summary <world>               the overview map and the most edited zones (SummaryCheck)
//   create <folder> <name> <seed> [radius] [--flat]
//                                 a new world from a seed, its middle generated radius zones out
//                                 (refuses a folder that already holds a world) (CreateCheck)
//   look <seed> [metres]          the biomes and water around the middle of a seed's world (LookCheck)
//   objects <world> [prefab]      the most common kinds of saved objects; with a prefab (name or
//                                 hash), each of its objects with its saved bytes, to check them
//                                 byte by byte (ObjectsCheck)
//   altbiomes <dump> <seed> [valheim folder]
//                                 the biome areas and alt biomes against those the game made
//                                 (the AltDump test plugin's file) (AltBiomesCheck)
//   locations <dump> <seed> [valheim folder]
//                                 every location against those the game laid out (the
//                                 AltDump test plugin's location file) (LocationsCheck)
//   regrow <world> <zones> [--no-alts] [--all-kinds]
//                                 Regrow nature on zones the game generated against what it
//                                 put there (the AltDump test plugin's zone file) (RegrowCheck)
//   contents <world> <bepinex folder> <name part>
//                                 New world's build of locations whose contents Expand World
//                                 Data changes, against those the game made (ContentsCheck)
//   vegetation <world> <bepinex folder> <zone file> <rules>
//                                 Regrow with the first rules of Expand World Data's vegetation
//                                 file (blueprints, data), against the game's (VegetationCheck)
//   snap <world> <name part>      the ground New world puts locations' SnapToGround objects on,
//                                 against where the game put them (SnapCheck)
if (args.Length < 2)
{
    Console.Error.WriteLine("usage: WorldCheck verify <dump> [seed] | verify-ingame <world> <file> | selftest-save <world copy> | inspect <world> | summary <world> | create <folder> <name> <seed> [radius] [--flat] | look <seed> [metres] | objects <world> [prefab] | altbiomes <dump> <seed> [valheim folder] | locations <dump> <seed> [valheim folder] | regrow <world> <zones> [--no-alts] [--all-kinds] | contents <world> <bepinex folder> <name part> | vegetation <world> <bepinex folder> <zone file> <rules> | snap <world> <name part>");
    return 2;
}
if (args[0] == "create")
{
    return CreateCheck.Run(args);
}
if (args[0] == "look")
{
    return LookCheck.Run(args);
}
if (args[0] == "altbiomes")
{
    return AltBiomesCheck.Run(args);
}
if (args[0] == "locations")
{
    return LocationsCheck.Run(args);
}
if (args[0] == "verify")
{
    _ = ValheimGen.DumpVerifier.Run(args[1], args.Length > 2 ? args[2] : "5DCcdIcuYJ");
    return 0;
}
string worldDir = args[1];
if (!Directory.Exists(worldDir))
{
    Console.Error.WriteLine($"World folder not found: {worldDir}");
    return 1;
}
var stopwatch = System.Diagnostics.Stopwatch.StartNew();
// The game's data, from this computer's Valheim (as the editor reads it).
if (!GameData.Ensure(GameBundles.ForGame(GameLook.FindValheim(AppSettings.Load().ValheimPath), GameLook.IndexFile)))
{
    Console.Error.WriteLine(GameData.Missing);
    return 1;
}
WorldSave.ModifierPrefabs = [.. TerrainEditor.Terrain.TerrainModifiers.NetworkPrefabHashes];
var world = WorldSave.Load(worldDir);
WorldSave.Builder = Builders.Default(world);
Console.WriteLine($"Loaded save #{world.SaveNumber}: {world.ObjectCount:N0} objects in {world.ChunkCount} chunks, {world.TerrainZones.Count} zones with terrain edits ({stopwatch.ElapsedMilliseconds} ms).");
stopwatch.Restart();
var modifiers = new TerrainEditor.Terrain.TerrainModifiers(world);
var terrain = new ValheimGen.TerrainService(world, modifiers);
Console.WriteLine($"World '{world.Name}', seed {world.SeedName} ({world.Seed}): {modifiers.Count} terrain modifiers ({stopwatch.ElapsedMilliseconds} ms).");
switch (args[0])
{
    case "verify-ingame":
        return IngameVerifyCheck.Run(args, world, terrain, modifiers);
    case "selftest-save":
        return SaveSelfTestCheck.Run(worldDir, world);
    case "inspect":
        return InspectCheck.Run(world);
    case "summary":
        return SummaryCheck.Run(world, terrain, stopwatch);
    case "objects":
        return ObjectsCheck.Run(args, world);
    case "regrow":
        return RegrowCheck.Run(args, world, terrain);
    case "contents":
        return ContentsCheck.Run(args, world);
    case "vegetation":
        return VegetationCheck.Run(args, world);
    case "snap":
        return SnapCheck.Run(args, world);
    default:
        Console.Error.WriteLine("unknown check " + args[0]);
        return 2;
}
