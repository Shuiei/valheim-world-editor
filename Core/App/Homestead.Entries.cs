namespace TerrainEditor.App;

/// <summary>
/// The blueprint library's entries: a blueprint's details as the player edits them, its list entry, and reading
/// the folder into that list.
/// </summary>
public static partial class Homestead
{
    /// <summary>
    /// What a player writes about a blueprint: its name, a description and tags (house, viking, stone…).
    /// Homestead reads the name; the description is PlanBuild's "#Description:" line and the tags the
    /// editor's own "#Tags:" line, which Homestead skips (any header line before "#Pieces").
    /// </summary>
    /// <param name="Name">The blueprint's name (#Name:).</param>
    /// <param name="Description">Its description (#Description:), empty when none.</param>
    /// <param name="Tags">Its tags (#Tags:).</param>
    public sealed record Details(string Name, string Description, List<string> Tags)
    {
        /// <summary>
        /// The tag that makes a blueprint a dungeon: pasted into a world, it goes 5000 m up behind an entrance on the
        /// ground, as a generated dungeon does (the Workshop's Dungeon switch sets it).
        /// </summary>
        public const string DungeonTag = "dungeon";

        /// <summary>Whether the blueprint is a dungeon (it carries <see cref="DungeonTag"/>, in any case).</summary>
        public bool Dungeon => Tags.Any(t => t.Equals(DungeonTag, StringComparison.OrdinalIgnoreCase));

        /// <summary>These details with the dungeon tag set or taken away.</summary>
        public Details WithDungeon(bool yes) => yes
            ? Dungeon ? this : this with { Tags = [.. Tags, DungeonTag] }
            : this with { Tags = [.. Tags.Where(t => !t.Equals(DungeonTag, StringComparison.OrdinalIgnoreCase))] };

        /// <summary>
        /// "house, Viking,stone ,house" → house, Viking, stone. Split on commas or semicolons, duplicates
        /// dropped ignoring case, 12 at most.
        /// </summary>
        public static List<string> ParseTags(string? text) => [.. (text ?? "").Split(',', ';').Select(t => t.Trim()).Where(t => t.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase).Take(12)];
    }

    /// <summary>
    /// One blueprint of the folder: its name, creator, description and tags, how many of each piece kind
    /// (for its cost) and its picture (.png beside it).
    /// </summary>
    /// <param name="Path">The .blueprint file.</param>
    /// <param name="Name">Its #Name:, else the file name.</param>
    /// <param name="Creator">Its #Creator:, if any.</param>
    /// <param name="World">The world it was saved in (#HomesteadWorld:), null when not given.</param>
    /// <param name="Pieces">How many pieces it has.</param>
    /// <param name="Picture">The .png beside it, null when there is none.</param>
    /// <param name="Saved">The file's last write time (local).</param>
    public sealed record Entry(string Path, string Name, string? Creator, string? World, int Pieces, string? Picture, DateTime Saved)
    {
        /// <summary>Its #Description:, empty when none.</summary>
        public string Description { get; init; } = "";
        /// <summary>Its #Tags:.</summary>
        public List<string> Tags { get; init; } = [];
        /// <summary>How many pieces of each prefab it has.</summary>
        public Dictionary<string, int> Kinds { get; init; } = [];
        /// <summary>Its name, description and tags, as the player edits them.</summary>
        public Details Details => new(Name, Description, Tags);

        /// <summary>
        /// Whether every word of a search (split on spaces) is somewhere in its name, creator, description or tags,
        /// ignoring case; an empty search matches every blueprint. The one filter of every blueprint list.
        /// </summary>
        public bool Matches(string? query) => (query ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries).All(w =>
            Name.Contains(w, StringComparison.OrdinalIgnoreCase) || (Creator ?? "").Contains(w, StringComparison.OrdinalIgnoreCase)
            || Description.Contains(w, StringComparison.OrdinalIgnoreCase) || Tags.Any(t => t.Contains(w, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>The readable blueprints of a folder, by name (empty when the folder does not exist).</summary>
    public static List<Entry> List(string folder)
    {
        if (!Directory.Exists(folder))
        {
            return [];
        }
        return [.. Directory.GetFiles(folder, "*.blueprint").Select(Read).OfType<Entry>().OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>One blueprint file (null when it cannot be read).</summary>
    public static Entry? Read(string file)
    {
        try
        {
            Entry entry = Read(file, File.ReadLines(file));
            string png = Path.ChangeExtension(file, ".png");
            return entry with { Picture = File.Exists(png) ? png : null, Saved = File.GetLastWriteTime(file) };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Caught(ex, "reading a Homestead blueprint (left out)");
            return null;
        }
    }
}
