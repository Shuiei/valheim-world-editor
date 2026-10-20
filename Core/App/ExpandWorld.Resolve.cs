using TerrainEditor.Save;
using ValheimGen;

namespace TerrainEditor.App;

// A world's rules, and the log line saying where they came from.
public static partial class ExpandWorld
{
    /// <summary>
    /// A world's rules: the game's own values while the Expand World switch is off (a world made with Expand World Size
    /// is noted in the log), else from the world file's keys and the mods' settings files (<see cref="Rules(IReadOnlyDictionary{string, string}, Found)"/>), said
    /// in the log.
    /// </summary>
    /// <param name="save">The world (its name and starting global keys).</param>
    /// <param name="settings">The editor's settings.</param>
    public static WorldRules Resolve(WorldSave save, AppSettings settings)
    {
        var keys = Keys(save.StartingGlobalKeys);
        if (!settings.ExpandWorld)
        {
            NoteSwitchOff(save.Name, keys.Count);
            return WorldRules.Vanilla;
        }
        var found = Find(settings);
        return Resolve(save, keys, found, ReadData(found));
    }

    /// <summary>The rules from a world's keys and a folder's files read already, said in the log.</summary>
    /// <param name="save">The world.</param>
    /// <param name="keys">The world's Expand World Size keys.</param>
    /// <param name="found">The folder's mods and files.</param>
    /// <param name="data">Expand World Data's settings and files from it.</param>
    private static WorldRules Resolve(WorldSave save, Dictionary<string, string> keys, Found found, DataFiles data)
    {
        NeedGameRules(data);
        var rules = Rules(keys, found, data, save.Zones?.GlobalKeys);
        Console.WriteLine($"Expand World: {save.Name}: {SizeSource(keys.Count, found)}; {FolderText(found)}; {Describe(rules)}.");
        NoteData(data, rules);
        if (keys.Count > 0 && !found.Size)
        {
            Console.WriteLine("Expand World: the world was made with Expand World Size, but no BepInEx folder the editor knows has the mod installed and on: it is shown as the game makes it without the mod (add the mod's folder in Settings).");
        }
        NoteGrid(save, rules);
        return rules;
    }

    /// <summary>
    /// The game's location rules read before a world's rules are worked out when its location or alt biome file needs
    /// them (they are converted against the game's): a world opens before anything else has asked for them. Waits for
    /// them (the world is opened away from the window); without them the files are not used, as the notes say.
    /// </summary>
    /// <param name="data">Expand World Data's files.</param>
    private static void NeedGameRules(DataFiles data)
    {
        if (SeedLocations.Available || (data.Locations is not { Count: > 0 } && data.AltBiomes is not { Count: > 0 }))
        {
            return;
        }
        try
        {
            GameLocations.UseGameRules(GameLook.Bundles).GetAwaiter().GetResult();
        }
        catch (Exception e)
        {
            // Run away from the caller's code: whatever reading them throws ends here, in the log.
            Log.Caught(e, "reading the game's location rules for the world's location and alt biome files (the files are not used)");
        }
    }

    /// <summary>
    /// Notes a save whose chunk files lie on another grid than the settings ask for: the game lays them out again at
    /// its next load; until then the editor reads and writes them as they lie.
    /// </summary>
    private static void NoteGrid(WorldSave save, WorldRules rules)
    {
        if (rules.GridWidth != save.GridWidth)
        {
            Console.WriteLine($"Expand World: {save.Name}'s chunk files are laid out on a {save.GridWidth} zone grid and the settings ask for {rules.GridWidth}: the game converts the save at its next load; the editor keeps the files' grid.");
        }
    }

    /// <summary>Says what was read of Expand World Data's files, and what the reading had to say.</summary>
    private static void NoteData(DataFiles data, WorldRules rules)
    {
        if (data.Any)
        {
            string biomes = Kind(data.Biomes, "", $"{data.Biomes?.Count ?? 0} entries ({rules.Biomes.Biomes.Count - BiomeTable.GamesBiomes.Count} new biomes)");
            string world = Kind(data.World, "the mod's default rules (no file)", $"{rules.World?.Count ?? 0} rules");
            string territories = Kind(data.Territories, "", $"{rules.Territories.Count}");
            string locations = Kind(data.Locations, "the game's (no file)", $"{rules.Locations?.Locations.Count ?? 0} kinds");
            string vegetation = Kind(data.Vegetation, "the game's (no file)", $"{rules.Vegetation?.Count ?? 0} rules");
            string alts = Kind(data.AltBiomes, "the game's (no file)", $"{rules.Locations?.AltBiomes.Count ?? 0}");
            string rooms = Kind(data.Rooms, "the game's (no file)", $"{rules.Dungeons?.Rooms?.Count ?? 0}");
            string dungeons = Kind(data.Dungeons, "the game's (no file)", $"{rules.Dungeons?.Dungeons.Count ?? 0}");
            Console.WriteLine($"Expand World Data: biomes {biomes}; world {world}{(rules.LegacyGeneration ? " (legacy generation)" : "")}; territories {territories}; locations {locations}; vegetation {vegetation}; alt biomes {alts}; rooms {rooms}; dungeons {dungeons}.");
        }
        foreach (string note in data.Notes)
        {
            Console.WriteLine("Expand World Data: " + note);
        }
    }

    /// <summary>
    /// One kind of data for the log: "off" when its switch is off, <paramref name="noFile"/> when it is on without a
    /// file (unless empty: the count then), else what was read of it.
    /// </summary>
    private static string Kind(IReadOnlyList<Entry>? entries, string noFile, string read) => entries == null ? "off" : entries.Count == 0 && noFile != "" ? noFile : read;

    /// <summary>Notes a world made with Expand World Size that opens with the game's own generation.</summary>
    private static void NoteSwitchOff(string world, int keys)
    {
        if (keys > 0)
        {
            Console.WriteLine($"Expand World: {world} was made with Expand World Size ({keys} settings in its world file), but the Expand World switch in Settings is off: generated as the game itself does.");
        }
    }

    /// <summary>Where the Size settings came from, for the log.</summary>
    private static string SizeSource(int keys, Found found) => !found.Size ? "no Size" : keys > 0 ? $"Size settings from the world file ({keys})" : found.SizeCfg != null ? "Size settings from " + found.SizeCfg : "no Size settings";

    /// <summary>The folder and the mods it has, for the log.</summary>
    private static string FolderText(Found found) => found.Any ? $"{found.Folder} ({found.Where}): Size {YesNo(found.Size)}, Data {YesNo(found.Data)}, Rivers {YesNo(found.Rivers)}" : "no BepInEx folder has Expand World";

    /// <summary>"yes" or "no".</summary>
    private static string YesNo(bool b) => b ? "yes" : "no";
}
