namespace ExpandWorld.WorldEditor;

// Expand World Data's settings file and data files, as read for a world.
public static partial class ExpandWorldMod
{
    /// <summary>One entry of a data file: its fields by name, scalars as text, nested lists and maps as such.</summary>
    public sealed class Entry(IReadOnlyDictionary<string, object?> fields)
    {
        /// <summary>The entry's fields, as written.</summary>
        public IReadOnlyDictionary<string, object?> Fields { get; } = fields;
    }

    /// <summary>
    /// Expand World Data's files for a world: its settings file's values and, for each kind of data whose switch is
    /// on, the entries of its files (every expand_&lt;kind&gt;*.yaml under the data folder, in reverse alphabetical
    /// order, as the mod reads them); null for a kind whose switch is off, or whose files could not be read at all.
    /// </summary>
    /// <param name="Cfg">The settings file's values by name (empty for none).</param>
    /// <param name="Biomes">The biome files' entries; null for off.</param>
    /// <param name="World">The world files' entries; null for off; empty for no file (the mod then makes its default one).</param>
    /// <param name="Territories">The territory files' entries; null for off.</param>
    /// <param name="Locations">The location files' entries; null for off; empty for no file (the mod then writes the game's).</param>
    /// <param name="Vegetation">The vegetation files' entries; null for off; empty for no file.</param>
    /// <param name="AltBiomes">The alt biome files' entries; null for off; empty for no file.</param>
    /// <param name="Notes">What the reading had to say (a file passed over, a value not understood), for the log.</param>
    public sealed record DataFiles(IReadOnlyDictionary<string, string> Cfg, IReadOnlyList<Entry>? Biomes, IReadOnlyList<Entry>? World, IReadOnlyList<Entry>? Territories,
        IReadOnlyList<Entry>? Locations, IReadOnlyList<Entry>? Vegetation, IReadOnlyList<Entry>? AltBiomes, List<string> Notes)
    {
        /// <summary>No settings, no data (the mod is not there, or every switch is off).</summary>
        public static DataFiles None => new(new Dictionary<string, string>(), null, null, null, null, null, null, []);

        /// <summary>
        /// The data entries the location file's "data" fields name (BepInEx/config/data/*.yaml, then
        /// expand_world/expand_data*.yaml); read only with the location file on.
        /// </summary>
        public IReadOnlyList<Entry> DataEntries { get; init; } = [];

        /// <summary>The room files' entries; null for off; empty for no file.</summary>
        public IReadOnlyList<Entry>? Rooms { get; init; }

        /// <summary>The dungeon files' entries; null for off; empty for no file.</summary>
        public IReadOnlyList<Entry>? Dungeons { get; init; }

        /// <summary>Whether any kind of data is on.</summary>
        public bool Any => Biomes != null || World != null || Territories != null || Locations != null || Vegetation != null || AltBiomes != null || Rooms != null || Dungeons != null;
    }

    /// <summary>
    /// The data kinds this version of the editor reads, and the switch of each in the settings file.
    /// </summary>
    private static readonly (string Kind, string Switch)[] Kinds =
    [
        ("biomes", "Biome data"), ("world", "World data"), ("territories", "Territory data"),
        ("locations", "Location data"), ("vegetation", "Vegetation data"), ("altbiomes", "Alt biome data"),
        ("rooms", "Room data"), ("dungeons", "Dungeon data"),
    ];

    /// <summary>Whether the kinds read can name data entries: the location and room files' objects do.</summary>
    /// <param name="kinds">Each kind's entries (<see cref="Kinds"/>' order), null for off.</param>
    private static bool NamesData(IReadOnlyList<Entry>?[] kinds) => kinds[3] != null || kinds[6] != null;

    /// <summary>
    /// Expand World Data's files from a BepInEx folder: the settings file, and the data files of each kind whose
    /// switch is on, read through the YamlDotNet library under the folder's plugins (none there: the data is not read,
    /// said in the notes). Nothing when the folder has no Expand World Data.
    /// </summary>
    /// <param name="found">The folder's mods and files.</param>
    public static DataFiles ReadData(Found found)
    {
        if (!found.Data)
        {
            return DataFiles.None;
        }
        var cfg = CfgOf(found.DataCfg);
        List<string> notes = [];
        bool[] on = [.. Kinds.Select(k => Bool(cfg.GetValueOrDefault(k.Switch), false))];
        if (!on.Any(b => b))
        {
            return new DataFiles(cfg, null, null, null, null, null, null, notes);
        }
        var yaml = YamlReader.Find(found.Plugins);
        if (yaml == null)
        {
            notes.Add($"no {YamlReader.DllName} under {found.Plugins} (Expand World Data needs it): its data files were not read");
        }
        IReadOnlyList<Entry>?[] kinds = [.. Kinds.Select((k, i) => on[i] ? ReadKind(found.DataFiles, k.Kind, yaml, notes) : null)];
        return new DataFiles(cfg, kinds[0], kinds[1], kinds[2], kinds[3], kinds[4], kinds[5], notes)
        {
            Rooms = kinds[6],
            Dungeons = kinds[7],
            DataEntries = NamesData(kinds) ? ReadDataEntries(found, yaml, notes) : [],
        };
    }

    /// <summary>
    /// The data entries (DataLoading.LoadEntries): every yaml file under BepInEx/config/data, then expand_data*.yaml
    /// under the data folder; none without a reader.
    /// </summary>
    private static List<Entry> ReadDataEntries(Found found, YamlReader? yaml, List<string> notes)
    {
        string data = Path.Combine(found.Folder, "config", "data");
        List<Entry> entries = [.. ReadFiles(Directory.Exists(data) ? FilesOf(data, "*.yaml", notes).Order(StringComparer.Ordinal) : [], yaml, notes)];
        entries.AddRange(ReadKind(found.DataFiles, "data", yaml, notes) ?? []);
        return entries;
    }

    /// <summary>
    /// The entries of one kind's files (expand_&lt;kind&gt;*.yaml under the data folder at any depth, in reverse
    /// alphabetical order of their paths, as the mod reads them); empty for no file; null when there are files and no
    /// reader. A file that cannot be read is passed over and noted.
    /// </summary>
    /// <param name="folder">The data folder; null for none.</param>
    /// <param name="kind">The kind ("biomes", "world"...).</param>
    /// <param name="yaml">The reader; null for none.</param>
    /// <param name="notes">Where a file passed over is noted.</param>
    internal static IReadOnlyList<Entry>? ReadKind(string? folder, string kind, YamlReader? yaml, List<string> notes)
    {
        List<string> files = DataFilesOf(folder, kind, notes);
        if (files.Count == 0)
        {
            return [];
        }
        if (yaml == null)
        {
            return null;
        }
        return ReadFiles(files, yaml, notes);
    }

    /// <summary>The entries of these files, in order; a file that cannot be read is passed over and noted.</summary>
    private static List<Entry> ReadFiles(IEnumerable<string> files, YamlReader? yaml, List<string> notes)
    {
        List<Entry> entries = [];
        if (yaml == null)
        {
            return entries;
        }
        foreach (string file in files)
        {
            try
            {
                entries.AddRange(yaml.ReadList(File.ReadAllText(file)).Select(map => new Entry(map)));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
            {
                Caught(ex, $"reading {file} (passed over)");
                notes.Add($"{Path.GetFileName(file)} could not be read and was passed over: {ex.Message}");
            }
        }
        return entries;
    }

    /// <summary>The files of a kind in the order the mod reads them (reverse alphabetical by full path).</summary>
    private static List<string> DataFilesOf(string? folder, string kind, List<string> notes) =>
        folder == null || !Directory.Exists(folder) ? [] : [.. FilesOf(folder, $"expand_{kind}*.yaml", notes).OrderByDescending(p => p, StringComparer.Ordinal)];

    /// <summary>The files of a folder that match a pattern, at any depth; none (noted) when it cannot be searched.</summary>
    private static List<string> FilesOf(string folder, string pattern, List<string> notes)
    {
        try
        {
            return [.. Directory.EnumerateFiles(folder, pattern, new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = 0 })];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Caught(ex, $"listing {folder} for {pattern} (taken as none)");
            notes.Add($"{folder} could not be searched for {pattern}: {ex.Message}");
            return [];
        }
    }
}
