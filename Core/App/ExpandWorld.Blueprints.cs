using System.Globalization;
using System.Numerics;
using ValheimGen;
using NVector3 = System.Numerics.Vector3;

namespace TerrainEditor.App;

// Expand World Data's blueprints (its Blueprints and BlueprintManager): the files it reads, and their objects as it
// centres them, for what the editor places in their stead.
public static partial class ExpandWorld
{
    /// <summary>The piece a PlanBuild blueprint is centred on unless its "#Center" names another.</summary>
    private const string DefaultCenterPiece = "piece_bpcenterpoint";

    /// <summary>
    /// A blueprint and the blueprints it nests, by name, each as its parts (BlueprintManager.Load); null when there is
    /// no blueprint of that name or it cannot be read (noted). A part that is neither a prefab of the game nor a
    /// blueprint cannot be made (why), nor one that is a blueprint holding the one it is in (the game would spawn it
    /// without end): it is left out.
    /// </summary>
    /// <param name="folder">The folder the mod reads blueprint files from; null for none.</param>
    /// <param name="name">The blueprint's name.</param>
    /// <param name="data">The world's data entries (what the parts' data names).</param>
    /// <param name="why">Where what cannot be made is said.</param>
    /// <param name="notes">Where a file that cannot be read is noted.</param>
    /// <param name="read">Where each blueprint's file is kept as it is read (its terrain snapshots); null for no need.</param>
    internal static Dictionary<string, IReadOnlyList<BlueprintPart>>? BlueprintsOf(string? folder, string name, DataTable data, List<string> why, List<string> notes,
        Dictionary<string, BlueprintFile>? read = null)
    {
        read ??= [];
        string[] files = BlueprintFiles(folder);
        var found = new Dictionary<string, IReadOnlyList<BlueprintPart>>(StringComparer.Ordinal);
        var loading = new HashSet<string>(StringComparer.Ordinal);
        return Load(name) ? found : null;

        bool Load(string bp)
        {
            if (found.ContainsKey(bp))
            {
                return true;
            }
            if (ReadBlueprint(files, bp, data, why, notes) is not { } file)
            {
                return false;
            }
            read[bp] = file;
            var parts = file.Parts;
            found[bp] = parts;
            loading.Add(bp);
            var holding = Holding(parts, bp, loading, why);
            foreach (var part in parts.Where(p => p.Chance > 0f && !holding.Contains(p.Prefab) && !Known(p.Prefab)))
            {
                if (!Load(part.Prefab))
                {
                    why.Add($"a {part.Prefab} in blueprint {bp} (no prefab of the game)");
                }
            }
            loading.Remove(bp);
            found[bp] = holding.Count == 0 ? parts : [.. parts.Where(p => !holding.Contains(p.Prefab))];
            return true;
        }
    }

    /// <summary>The parts of a blueprint naming one it is held in (being loaded): each said, to be left out.</summary>
    private static HashSet<string> Holding(List<BlueprintPart> parts, string bp, HashSet<string> loading, List<string> why)
    {
        var holding = parts.Select(p => p.Prefab).Where(loading.Contains).ToHashSet(StringComparer.Ordinal);
        why.AddRange(holding.Select(inner => $"blueprint {bp} holding {inner}, which holds it"));
        return holding;
    }

    /// <summary>The file name extensions of the blueprint files the mod reads.</summary>
    private static readonly string[] BlueprintExtensions = [".blueprint", ".vbuild"];

    /// <summary>
    /// The blueprint files the mod looks in (Blueprints.Files): every .blueprint and .vbuild at any depth of the
    /// folder, in name order; none without the folder.
    /// </summary>
    private static string[] BlueprintFiles(string? folder) =>
        folder == null || !Directory.Exists(folder) ? [] :
        [.. Directory.EnumerateFiles(folder, "*.blueprint", SearchOption.AllDirectories).Concat(Directory.EnumerateFiles(folder, "*.vbuild", SearchOption.AllDirectories)).Distinct().Order(StringComparer.Ordinal)];

    /// <summary>
    /// A blueprint file as the mod reads it (Blueprint): its parts centred, how far it reaches from its centre on the
    /// ground (its pieces' bounds, or its terrain snapshots' when wider), and its terrain snapshots with values.
    /// </summary>
    /// <param name="Parts">Its parts, centred.</param>
    /// <param name="Radius">Metres it reaches from its centre on the ground.</param>
    /// <param name="Heights">Its height snapshot with values (a blueprint location then levels its ground by it); null for none.</param>
    /// <param name="Paints">Its paint snapshot with values; null for none.</param>
    internal sealed record BlueprintFile(List<BlueprintPart> Parts, float Radius, TerrainSnapshot? Heights, TerrainSnapshot? Paints);

    /// <summary>
    /// One blueprint as the mod reads and centres it (Blueprints.GetBluePrint, Blueprint.Center): the first file whose
    /// name, spaces as underscores, is the name; null when none is, or it cannot be read (noted).
    /// </summary>
    private static BlueprintFile? ReadBlueprint(string[] files, string name, DataTable data, List<string> why, List<string> notes)
    {
        string wanted = name.Replace(' ', '_');
        string? file = files.FirstOrDefault(f => Path.GetFileNameWithoutExtension(f).Replace(' ', '_') == wanted);
        if (file == null)
        {
            return null;
        }
        try
        {
            return Centred(FileParts(file, data, why));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException or OverflowException)
        {
            Log.Caught(ex, $"reading the blueprint {file}");
            notes.Add($"the blueprint {Path.GetFileName(file)} could not be read: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// A blueprint file's contents by its extension, as the mod tells them (Blueprints.GetBluePrint, exactly as written):
    /// ".vbuild" a BuildShare file, ".blueprint" a PlanBuild one; any other spelling the mod finds (case does not matter
    /// when Windows lists the files) it refuses, and so does this.
    /// </summary>
    /// <exception cref="FormatException">The extension is neither, or a row cannot be read.</exception>
    private static PlanBuild FileParts(string file, DataTable data, List<string> why) => Path.GetExtension(file) switch
    {
        ".vbuild" => new PlanBuild(BuildShareParts(File.ReadAllLines(file), data, why), DefaultCenterPiece, null, null),
        ".blueprint" => PlanBuildParts(File.ReadAllLines(file), data, why),
        _ => throw new FormatException($"the mod reads only \".blueprint\" and \".vbuild\" files, written so, not \"{Path.GetExtension(file)}\""),
    };

    /// <summary>What a blueprint file holds before it is centred.</summary>
    /// <param name="Parts">Its pieces as the file has them.</param>
    /// <param name="Center">The piece it is centred on.</param>
    /// <param name="Heights">Its height snapshot, when it has one.</param>
    /// <param name="Paints">Its paint snapshot, when it has one.</param>
    private sealed record PlanBuild(List<BlueprintPart> Parts, string Center, Snapshot? Heights, Snapshot? Paints);

    /// <summary>
    /// A PlanBuild file (Blueprints.GetPlanBuild): the rows of its "#Pieces" part (the rows before any header too),
    /// "name;category;x;y;z;rx;ry;rz;rw;info;sx;sy;sz;data;chance"; the piece it is centred on ("#Center"); its terrain
    /// snapshots ("#TerrainHeight", "#TerrainPaint"; one whose header the mod cannot read is passed over).
    /// </summary>
    private static PlanBuild PlanBuildParts(string[] rows, DataTable data, List<string> why)
    {
        var reading = new PlanBuildReading(data, why);
        foreach (string row in rows)
        {
            reading.Read(row);
        }
        return new PlanBuild(reading.Parts, reading.Center, reading.Heights, reading.Paints);
    }

    /// <summary>A PlanBuild file read row by row: the part each row is in, and what the parts gave so far.</summary>
    /// <param name="data">The world's data entries (what the pieces' data names).</param>
    /// <param name="why">Where data that cannot be written is said.</param>
    private sealed class PlanBuildReading(DataTable data, List<string> why)
    {
        /// <summary>The section rows are in: the pieces (the first), a snapshot's, or one passed over.</summary>
        private bool _pieces = true, _paint;

        /// <summary>The snapshot the rows fill, when they are one's.</summary>
        private Snapshot? _section;

        /// <summary>The pieces read.</summary>
        public List<BlueprintPart> Parts { get; } = [];

        /// <summary>The piece the blueprint is centred on.</summary>
        public string Center { get; private set; } = DefaultCenterPiece;

        /// <summary>Its height snapshot, when it has one.</summary>
        public Snapshot? Heights { get; private set; }

        /// <summary>Its paint snapshot, when it has one.</summary>
        public Snapshot? Paints { get; private set; }

        /// <summary>Takes a row: a header starts a section, any other row belongs to the current one.</summary>
        public void Read(string row)
        {
            if (row.StartsWith('#'))
            {
                Header(row);
            }
            else if (_pieces)
            {
                string[] s = row.Replace(',', '.').Split(';');
                Parts.Add(Part(s[0], new NVector3(Num(s, 2), Num(s, 3), Num(s, 4)), new Quaternion(Num(s, 5), Num(s, 6), Num(s, 7), Num(s, 8)),
                    new NVector3(Num(s, 10, 1f), Num(s, 11, 1f), Num(s, 12, 1f)), s.Length > 13 ? s[13] : "", Num(s, 14, 1f), data, why));
            }
            else
            {
                _section?.Add(SnapshotRow(row, _paint));
            }
        }

        /// <summary>A header (ReadPlanBuildHeader): the section it starts; "#Center" names the centre piece.</summary>
        private void Header(string row)
        {
            int colon = row.IndexOf(':', StringComparison.Ordinal);
            string header = (colon < 0 ? row : row[..colon]).Trim().ToLowerInvariant();
            _pieces = header == "#pieces";
            _paint = header == "#terrainpaint";
            _section = header is "#terrainheight" or "#terrainpaint" ? SnapshotOf(row, colon) : null;
            if (_section != null)
            {
                (Heights, Paints) = _paint ? (Heights, _section) : (_section, Paints);
            }
            if (header == "#center")
            {
                Center = colon < 0 ? throw new FormatException("#Center names no piece") : row[(colon + 1)..];
            }
        }
    }

    /// <summary>A BuildShare file's objects (Blueprints.GetBuildShare): one a row, "name rx ry rz rw x y z data chance".</summary>
    private static List<BlueprintPart> BuildShareParts(string[] rows, DataTable data, List<string> why) =>
        [.. rows.Select(row => row.Replace(',', '.').Split(' ')).Select(s => Part(s[0], new NVector3(Num(s, 5), Num(s, 6), Num(s, 7)),
            new Quaternion(Num(s, 1), Num(s, 2), Num(s, 3), Num(s, 4)), NVector3.One, s.Length > 8 ? s[8] : "", s.Length > 9 ? Num(s, 9, 1f) : 1f, data, why))];

    /// <summary>One part, its turn made of length 1 and its data read (data that cannot be written: why).</summary>
    private static BlueprintPart Part(string prefab, NVector3 position, Quaternion rotation, NVector3 scale, string dataName, float chance, DataTable data, List<string> why)
    {
        var values = data.Resolve(dataName);
        if (values.Why != null)
        {
            why.Add(values.Why);
        }
        return new BlueprintPart(prefab, position, Normalized(rotation), scale, values.OrNull, chance);
    }

    /// <summary>A rotation made of length 1 as Unity does (none for one too short to tell).</summary>
    private static Quaternion Normalized(Quaternion q)
    {
        float length = MathF.Sqrt(Quaternion.Dot(q, q));
        return length < float.Epsilon ? Quaternion.Identity : new Quaternion(q.X / length, q.Y / length, q.Z / length, q.W / length);
    }

    /// <summary>A number of a row as the mod reads it (InvariantFloat): the default when missing or empty; anything else not a number fails the file.</summary>
    private static float Num(string[] s, int index, float fallback = 0f) =>
        index >= s.Length || string.IsNullOrEmpty(s[index]) ? fallback : float.Parse(s[index], NumberStyles.Any, NumberFormatInfo.InvariantInfo);

    /// <summary>
    /// A blueprint centred as the mod centres it (Blueprint.Center): on its centre piece when it has one (that piece
    /// then never made, the others turned by its turn taken back), else on the middle of their bounds on the ground
    /// (bounds that hold the file's origin too, as Unity's start from it) at 5 cm above the lowest; its radius half the
    /// diagonal of those bounds on the ground, or its snapshots' reach when wider (a snapshot reaching too far, or two
    /// reaching too far together, dropped as the mod drops them).
    /// </summary>
    private static BlueprintFile Centred(PlanBuild read)
    {
        var parts = read.Parts;
        var min = parts.Aggregate(NVector3.Zero, (m, p) => NVector3.Min(m, p.Position));
        var max = parts.Aggregate(NVector3.Zero, (m, p) => NVector3.Max(m, p.Position));
        float ex = (max.X - min.X) * 0.5f, ez = (max.Z - min.Z) * 0.5f;
        float radius = (float)Math.Sqrt((double)ex * ex + (double)ez * ez);
        var center = parts.Count == 0 ? NVector3.Zero : new NVector3((min.X + max.X) * 0.5f, parts.Min(p => p.Position.Y) + 0.05f, (min.Z + max.Z) * 0.5f);
        var turn = Quaternion.Identity;
        int at = parts.FindIndex(p => p.Prefab == read.Center);
        if (at >= 0)
        {
            center = parts[at].Position;
            turn = Quaternion.Inverse(parts[at].Rotation);
            parts[at] = parts[at] with { Chance = 0f };
        }
        // Unity's != on rotations: the same unless their dot product is below 0.999999.
        bool turned = Quaternion.Dot(turn, Quaternion.Identity) <= 0.999999f;
        List<BlueprintPart> centred = [.. parts.Select(p => turned
            ? p with { Position = NVector3.Transform(p.Position - center, turn), Rotation = turn * p.Rotation }
            : p with { Position = p.Position - center })];
        var (heights, paints, reach) = Reaches(read.Heights, read.Paints, center, turn);
        return new BlueprintFile(centred, MathF.Max(radius, reach), heights, paints);
    }

    /// <summary>
    /// The snapshots moved with the blueprint's centre and turn: each kept with values, and how far the farther reaches.
    /// One reaching too far is dropped; both are when, with values, they reach too far together.
    /// </summary>
    private static (TerrainSnapshot? Heights, TerrainSnapshot? Paints, float Reach) Reaches(Snapshot? heights, Snapshot? paints, NVector3 center, Quaternion turn)
    {
        var (h, p) = (Kept(heights, center, turn, center.Y), Kept(paints, center, turn, 0f));
        float reach = MathF.Max(h.Reach, p.Reach), margin = MathF.Max(1f, MathF.Max(h.Spacing, p.Spacing));
        return h.Kept != null && p.Kept != null && reach + margin > MaxSnapshotReach ? (null, null, 0f) : (h.Kept, p.Kept, reach);
    }

    /// <summary>
    /// A snapshot moved with the blueprint's centre and turn: kept when it has values, how far it reaches, and its node
    /// spacing when it has values (what the reach of both is measured with); nothing for none, or one reaching too far.
    /// </summary>
    private static (TerrainSnapshot? Kept, float Reach, float Spacing) Kept(Snapshot? snapshot, NVector3 center, Quaternion turn, float lower)
    {
        if (snapshot == null)
        {
            return default;
        }
        snapshot.MoveBy(center, turn, lower);
        return snapshot.Reach() is { } reach ? (snapshot.Values, reach, snapshot.HasValues ? snapshot.Spacing : 0f) : default;
    }
}
