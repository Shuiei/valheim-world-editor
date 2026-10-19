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
        var data = ReadData(found);
        var rules = Rules(keys, found, data);
        Console.WriteLine($"Expand World: {save.Name}: {SizeSource(keys.Count, found)}; {FolderText(found)}; {Describe(rules)}.");
        NoteData(data, rules);
        if (keys.Count > 0 && !found.Any)
        {
            Console.WriteLine("Expand World: the world was made with Expand World Size, but no BepInEx folder the editor knows has the mod: its Rivers and Data settings are unknown (add the folder in Settings).");
        }
        NoteGrid(save, rules);
        return rules;
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
            string biomes = data.Biomes == null ? "off" : $"{data.Biomes.Count} entries ({rules.Biomes.Biomes.Count - BiomeTable.GamesBiomes.Count} new biomes)";
            string world = data.World == null ? "off" : data.World.Count == 0 ? "the mod's default rules (no file)" : $"{rules.World?.Count ?? 0} rules";
            string territories = data.Territories == null ? "off" : $"{rules.Territories.Count}";
            Console.WriteLine($"Expand World Data: biomes {biomes}; world {world}{(rules.LegacyGeneration ? " (legacy generation)" : "")}; territories {territories}.");
        }
        foreach (string note in data.Notes)
        {
            Console.WriteLine("Expand World Data: " + note);
        }
    }

    /// <summary>Notes a world made with Expand World Size that opens with the game's own generation.</summary>
    private static void NoteSwitchOff(string world, int keys)
    {
        if (keys > 0)
        {
            Console.WriteLine($"Expand World: {world} was made with Expand World Size ({keys} settings in its world file), but the Expand World switch in Settings is off: generated as the game itself does.");
        }
    }

    /// <summary>Where the Size settings came from, for the log.</summary>
    private static string SizeSource(int keys, Found found) => keys > 0 ? $"Size settings from the world file ({keys})" : found.SizeCfg != null ? "Size settings from " + found.SizeCfg : "no Size settings";

    /// <summary>The folder and the mods it has, for the log.</summary>
    private static string FolderText(Found found) => found.Any ? $"{found.Folder} ({found.Where}): Size {YesNo(found.Size)}, Data {YesNo(found.Data)}, Rivers {YesNo(found.Rivers)}" : "no BepInEx folder has Expand World";

    /// <summary>"yes" or "no".</summary>
    private static string YesNo(bool b) => b ? "yes" : "no";
}
