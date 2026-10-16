using System.Buffers;
using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace TerrainEditor.App;

/// <summary>
/// Homestead (sighsorry-Homestead), the in-game building mod: its blueprints are the editor's building
/// library. They are PlanBuild .blueprint files with Homestead's own header lines, a .png picture
/// beside each, in Homestead/Blueprints of Valheim's save folder (next to worlds_local), where Homestead
/// lists them in its hammer tab and players build them in game with a materials chest. Each piece is
/// "prefab;category;x;y;z;qx;qy;qz;qw;"text";sx;sy;sz", measured from the blueprint's anchor: the
/// middle of the saved area at ground level, turned with it (ZoneBlueprintFileFormat.cs, Homestead's
/// source on GitHub).
/// </summary>
public static class Homestead
{
    /// <summary>Homestead's Thunderstore page, for players who do not have it.</summary>
    public const string PageUrl = "https://thunderstore.io/c/valheim/p/sighsorry/Homestead/";
    /// <summary>The #HomesteadVersion written into blueprints.</summary>
    public const int FormatVersion = 1;

    /// <summary>
    /// Whether Homestead is installed (in the game's BepInEx or a mod manager profile, its version and
    /// where), and its blueprint folder (made when the first blueprint is saved).
    /// </summary>
    /// <param name="Installed">Whether a Homestead.dll was found in any BepInEx plugins folder.</param>
    /// <param name="Version">The first found copy's version, null when unknown.</param>
    /// <param name="Where">The places (game, profile names) that have it.</param>
    /// <param name="Folder">The blueprint folder (<c>Homestead.Folder()</c>), which may not exist yet.</param>
    public sealed record Status(bool Installed, string? Version, List<string> Where, string Folder);

    /// <summary>Looks for Homestead.dll in every BepInEx folder <c>LocalGame.BepInExFolders</c> knows.</summary>
    public static Status Find(AppSettings settings)
    {
        var where = new List<string>();
        string? version = null;
        foreach (var (dir, place) in LocalGame.BepInExFolders(settings))
        {
            string plugins = Path.Combine(dir, "plugins");
            if (!Directory.Exists(plugins))
            {
                continue;
            }
            try
            {
                foreach (string dll in Directory.EnumerateFiles(plugins, "Homestead.dll", SearchOption.AllDirectories))
                {
                    where.Add(place);
                    version ??= VersionOf(dll);
                    break;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }
        }
        return new Status(where.Count > 0, version, where, Folder());
    }

    /// <summary>
    /// The mod package's manifest.json (as mod managers install it), else the DLL's own version; null when
    /// neither can be read.
    /// </summary>
    private static string? VersionOf(string dll)
    {
        try
        {
            string manifest = Path.Combine(Path.GetDirectoryName(dll)!, "manifest.json");
            if (File.Exists(manifest) && JsonNode.Parse(File.ReadAllText(manifest))?["version_number"] is JsonNode v)
            {
                return (string?)v;
            }
            return FileVersionInfo.GetVersionInfo(dll).FileVersion;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException or InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>Another folder than the player's (the tests': they must never write into the game's folders).</summary>
    public static string? FolderOverride { get; set; }

    /// <summary>
    /// Homestead/Blueprints in Valheim's save folder: the one that has it already, else the first save
    /// folder the game made (where its worlds are), else the usual one for this system.
    /// </summary>
    public static string Folder()
    {
        if (FolderOverride != null)
        {
            return FolderOverride;
        }
        var saves = Places.WorldRoots().Select(r => Path.GetDirectoryName(r)!).Distinct().ToList();
        static string Of(string save) => Path.Combine(save, "Homestead", "Blueprints");
        return saves.Select(Of).FirstOrDefault(Directory.Exists) ?? Of(saves.FirstOrDefault(Directory.Exists) ?? saves[0]);
    }

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
            return null;
        }
    }

    /// <summary>A blueprint from its lines, without its picture or time yet.</summary>
    /// <param name="file">Its path, for the entry's name when it has no #Name:.</param>
    /// <param name="lines">The file's lines.</param>
    public static Entry Read(string file, IEnumerable<string> lines)
    {
        string? name = null, creator = null, world = null, description = null, tags = null;
        int pieces = 0;
        var kinds = new Dictionary<string, int>();
        bool inPieces = false;
        foreach (string raw in lines)
        {
            string line = raw.Trim();
            if (line.StartsWith("#Name:", StringComparison.OrdinalIgnoreCase))
            {
                name = line[6..].Trim();
            }
            else if (line.StartsWith("#Creator:", StringComparison.OrdinalIgnoreCase))
            {
                creator = line[9..].Trim();
            }
            else if (line.StartsWith("#Description:", StringComparison.OrdinalIgnoreCase))
            {
                description = line[13..].Trim().Trim('"');
            }
            else if (line.StartsWith("#Tags:", StringComparison.OrdinalIgnoreCase))
            {
                tags = line[6..];
            }
            else if (line.StartsWith("#HomesteadWorld:", StringComparison.OrdinalIgnoreCase))
            {
                world = line[16..].Trim();
            }
            else if (line.StartsWith('#'))
            {
                inPieces = line.Equals("#Pieces", StringComparison.OrdinalIgnoreCase);
            }
            else if (inPieces && line.Length > 0)
            {
                pieces++;
                string kind = line.Split(';')[0].Trim();
                kinds[kind] = kinds.GetValueOrDefault(kind) + 1;
            }
        }
        return new Entry(file, string.IsNullOrWhiteSpace(name) ? Path.GetFileNameWithoutExtension(file) : name, creator,
            string.IsNullOrWhiteSpace(world) ? null : world, pieces, null, default)
        {
            Description = description ?? "",
            Tags = Details.ParseTags(tags),
            Kinds = kinds,
        };
    }

    /// <summary>
    /// A blueprint's name, description and tags changed in its file (the pieces stay as they are). The name
    /// goes first, the description and tags after #Creator: (else after the name).
    /// </summary>
    /// <param name="text">The blueprint file's text.</param>
    /// <param name="details">What to write; no #Tags: line when it has no tags.</param>
    /// <returns>The new text, with \n line ends.</returns>
    public static string WithDetails(string text, Details details)
    {
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n').ToList();
        int pieces = lines.FindIndex(l => l.Trim().Equals("#Pieces", StringComparison.OrdinalIgnoreCase));
        if (pieces < 0)
        {
            pieces = lines.Count;
        }
        // Matched as Read matches them, spaces before the # aside (a hand-edited " #Name: Old" stayed, and won).
        var head = lines.Take(pieces).Where(l => !(l.TrimStart().StartsWith("#Name:", StringComparison.OrdinalIgnoreCase) || l.TrimStart().StartsWith("#Description:", StringComparison.OrdinalIgnoreCase)
            || l.TrimStart().StartsWith("#Tags:", StringComparison.OrdinalIgnoreCase))).ToList();
        var mine = new List<string> { "#Name:" + Header(details.Name), "#Description:" + Header(details.Description) };
        if (details.Tags.Count > 0)
        {
            mine.Add("#Tags:" + Header(string.Join(", ", details.Tags)));
        }
        int creator = head.FindIndex(l => l.TrimStart().StartsWith("#Creator:", StringComparison.OrdinalIgnoreCase));
        head.InsertRange(0, mine.Take(1));
        head.InsertRange(creator < 0 ? 1 : creator + 2, mine.Skip(1));
        return string.Join("\n", head.Concat(lines.Skip(pieces)));
    }

    /// <summary>Characters a blueprint's file name keeps before ".blueprint".</summary>
    private const int FileNameLength = 64;

    /// <summary>The characters a file name cannot hold on Windows or Linux.</summary>
    private static readonly SearchValues<char> NotInFileNames = SearchValues.Create([.. Path.GetInvalidFileNameChars(), '<', '>', ':', '"', '/', '\\', '|', '?', '*']);

    /// <summary>
    /// A file name for a blueprint name: what Windows and Linux both allow. Forbidden characters become _, dots and
    /// spaces at the ends are dropped (after the cut too: Windows drops a trailing dot itself, and the picture's name
    /// would no longer match), at most 64 characters before ".blueprint", "blueprint" when nothing is left, and "_"
    /// before a name Windows keeps for a device (CON, NUL…).
    /// </summary>
    public static string FileName(string name)
    {
        string f = new string([.. name.Trim().Select(c => NotInFileNames.Contains(c) ? '_' : c)]).Trim('.', ' ');
        f = (f.Length > FileNameLength ? f[..FileNameLength] : f).TrimEnd('.', ' ');
        if (f.Length == 0)
        {
            f = "blueprint";
        }
        return (SafeFile.IsDeviceName(f) ? "_" + f : f) + ".blueprint";
    }

    /// <summary>
    /// The name itself when <paramref name="taken"/> says it is free, else the first "name (n)" (n from 2) that is.
    /// A long name is cut so that the number survives <see cref="FileName"/>'s 64-character cut: without that, every
    /// numbered name of a 64-character blueprint would give the same file and the search would never end.
    /// </summary>
    /// <param name="name">The wanted name.</param>
    /// <param name="taken">Whether a blueprint of that name exists already.</param>
    public static string FreeName(string name, Func<string, bool> taken)
    {
        name = name.Trim();
        if (!taken(name))
        {
            return name;
        }
        for (int n = 2; ; n++)
        {
            string suffix = $" ({n})";
            string stem = name.Length + suffix.Length > FileNameLength ? name[..(FileNameLength - suffix.Length)].TrimEnd() : name;
            string numbered = stem + suffix;
            if (!taken(numbered))
            {
                return numbered;
            }
        }
    }

    /// <summary>
    /// The objects of a copy (the editor's clipboard format) as a Homestead blueprint. dx, dz are metres
    /// from the copy's middle, dy from its ground: Homestead's anchor. Rotations become quaternions.
    /// </summary>
    /// <param name="clip">The copy, with its "objects" array.</param>
    /// <param name="name">The blueprint's name.</param>
    /// <param name="creator">Who made it.</param>
    /// <param name="world">The world it comes from, if known.</param>
    /// <param name="saved">When it was saved, written with its UTC offset.</param>
    /// <param name="description">Its description.</param>
    /// <param name="tags">Its tags; no #Tags: line when null or empty.</param>
    /// <returns>The .blueprint file's text, pieces ordered by height then x then z.</returns>
    public static string Write(JsonObject clip, string name, string creator, string? world, DateTime saved, string description = "", IReadOnlyCollection<string>? tags = null)
    {
        var objs = (clip["objects"] as JsonArray ?? []).OfType<JsonObject>().Select(o => (
            Name: (string?)o["name"] ?? "", X: F(o["dx"]), Y: F(o["dy"]), Z: F(o["dz"]),
            Rot: BlueprintFormats.FromEuler(new Vector3(F(o["rx"]), F(o["ry"]), F(o["rz"]))), Scale: F(o["scale"]),
            Data: BlueprintFormats.DataOf(o["data"]) ?? [])).Where(o => o.Name.Length > 0).ToList();
        // Written as a ruin when every object is (no builder): one line for all, not one each.
        bool ruin = objs.Count > 0 && objs.All(o => o.Data.Contains(Editing.ObjectField.NoBuilder));
        var ordered = objs.OrderBy(o => o.Y).ThenBy(o => o.X).ThenBy(o => o.Z).ToList();
        float radius = objs.Count == 0 ? 0 : MathF.Sqrt(objs.Max(o => o.X * o.X + o.Z * o.Z)) + 1f;
        StringBuilder sb = new();
        sb.Append("#Name:").Append(Header(name)).Append('\n');
        sb.Append("#Creator:").Append(Header(creator)).Append('\n');
        sb.Append("#Description:").Append(Header(description)).Append('\n');
        if (tags is { Count: > 0 })
        {
            sb.Append("#Tags:").Append(Header(string.Join(", ", tags))).Append('\n');
        }
        sb.Append("#Category:Blueprints\n");
        sb.Append("#HomesteadVersion:").Append(FormatVersion.ToString(CultureInfo.InvariantCulture)).Append('\n');
        sb.Append("#HomesteadWorld:").Append(Header(world ?? "")).Append('\n');
        sb.Append("#HomesteadSavedAt:").Append(new DateTimeOffset(saved).ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture)).Append('\n');
        sb.Append("#HomesteadRadius:").Append(S(radius)).Append('\n');
        // The editor's own: what objects hold (a chest's contents, a sign's text...), by their line below.
        if (ruin)
        {
            sb.Append(BlueprintFormats.RuinHeader).Append('\n');
        }
        for (int k = 0; k < ordered.Count; k++)
        {
            foreach (var f in ordered[k].Data.Where(f => !(ruin && f == Editing.ObjectField.NoBuilder)))
            {
                sb.Append(BlueprintFormats.DataHeader).Append(k.ToString(CultureInfo.InvariantCulture)).Append(';').Append(f.ToString()).Append('\n');
            }
        }
        sb.Append("#Pieces\n");
        foreach (var (prefab, x, y, z, rot, scale, data) in ordered)
        {
            float s = scale > 0 ? scale : 1f;
            sb.Append(string.Join(";", Header(prefab).Replace(";", "", StringComparison.Ordinal), "Building", S(x), S(y), S(z),
                S(rot.X), S(rot.Y), S(rot.Z), S(rot.W), "\"\"", S(s), S(s), S(s))).Append('\n');
        }
        return sb.ToString();
    }

    /// <summary>A JSON number as a float, 0 when missing.</summary>
    private static float F(JsonNode? n) => n == null ? 0 : float.Parse(n.ToJsonString(), CultureInfo.InvariantCulture);

    /// <summary>Homestead writes numbers with three decimals at most.</summary>
    private static string S(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);

    /// <summary>A value made one header line: line breaks become spaces, trimmed.</summary>
    private static string Header(string v) => v.Replace('\r', ' ').Replace('\n', ' ').Trim();
}
