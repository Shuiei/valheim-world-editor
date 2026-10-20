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
    /// blueprint cannot be made (why).
    /// </summary>
    /// <param name="folder">The folder the mod reads blueprint files from; null for none.</param>
    /// <param name="name">The blueprint's name.</param>
    /// <param name="data">The world's data entries (what the parts' data names).</param>
    /// <param name="why">Where what cannot be made is said.</param>
    /// <param name="notes">Where a file that cannot be read is noted.</param>
    internal static Dictionary<string, IReadOnlyList<BlueprintPart>>? BlueprintsOf(string? folder, string name, DataTable data, List<string> why, List<string> notes)
    {
        string[] files = BlueprintFiles(folder);
        var found = new Dictionary<string, IReadOnlyList<BlueprintPart>>(StringComparer.Ordinal);
        return Load(name) ? found : null;

        bool Load(string bp)
        {
            if (found.ContainsKey(bp))
            {
                return true;
            }
            if (ReadBlueprint(files, bp, data, why, notes) is not { } parts)
            {
                return false;
            }
            found[bp] = parts;
            foreach (var part in parts.Where(p => p.Chance != 0f && !Known(p.Prefab)))
            {
                if (!Load(part.Prefab))
                {
                    why.Add($"a {part.Prefab} in blueprint {bp} (no prefab of the game)");
                }
            }
            return true;
        }
    }

    /// <summary>
    /// The blueprint files the mod looks in (Blueprints.Files): every .blueprint and .vbuild at any depth of the
    /// folder, in name order; none without the folder.
    /// </summary>
    private static string[] BlueprintFiles(string? folder) =>
        folder == null || !Directory.Exists(folder) ? [] :
        [.. Directory.EnumerateFiles(folder, "*.blueprint", SearchOption.AllDirectories).Concat(Directory.EnumerateFiles(folder, "*.vbuild", SearchOption.AllDirectories)).Distinct().Order(StringComparer.Ordinal)];

    /// <summary>
    /// One blueprint's parts as the mod reads and centres them (Blueprints.GetBluePrint, Blueprint.Center): the first
    /// file whose name, spaces as underscores, is the name; null when none is, or it cannot be read (noted).
    /// </summary>
    private static List<BlueprintPart>? ReadBlueprint(string[] files, string name, DataTable data, List<string> why, List<string> notes)
    {
        string wanted = name.Replace(' ', '_');
        string? file = files.FirstOrDefault(f => Path.GetFileNameWithoutExtension(f).Replace(' ', '_') == wanted);
        if (file == null)
        {
            return null;
        }
        try
        {
            string[] rows = File.ReadAllLines(file);
            var (parts, center) = Path.GetExtension(file) == ".vbuild" ? (BuildShareParts(rows, data, why), DefaultCenterPiece) : PlanBuildParts(rows, data, why);
            return Centred(parts, center);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException or OverflowException)
        {
            Log.Caught(ex, $"reading the blueprint {file}");
            notes.Add($"the blueprint {Path.GetFileName(file)} could not be read: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// A PlanBuild file's pieces (Blueprints.GetPlanBuild): the rows of its "#Pieces" part (the rows before any header
    /// too), "name;category;x;y;z;rx;ry;rz;rw;info;sx;sy;sz;data;chance"; and the piece it is centred on ("#Center").
    /// </summary>
    private static (List<BlueprintPart> Parts, string Center) PlanBuildParts(string[] rows, DataTable data, List<string> why)
    {
        List<BlueprintPart> parts = [];
        string center = DefaultCenterPiece;
        bool pieces = true;
        foreach (string row in rows)
        {
            if (row.StartsWith('#'))
            {
                int colon = row.IndexOf(':', StringComparison.Ordinal);
                string header = (colon < 0 ? row : row[..colon]).Trim();
                pieces = header.Equals("#pieces", StringComparison.OrdinalIgnoreCase);
                if (header.Equals("#center", StringComparison.OrdinalIgnoreCase))
                {
                    center = colon < 0 ? throw new FormatException("#Center names no piece") : row[(colon + 1)..];
                }
                continue;
            }
            if (pieces)
            {
                string[] s = row.Replace(',', '.').Split(';');
                parts.Add(Part(s[0], new NVector3(Num(s, 2), Num(s, 3), Num(s, 4)), new Quaternion(Num(s, 5), Num(s, 6), Num(s, 7), Num(s, 8)),
                    new NVector3(Num(s, 10, 1f), Num(s, 11, 1f), Num(s, 12, 1f)), s.Length > 13 ? s[13] : "", Num(s, 14, 1f), data, why));
            }
        }
        return (parts, center);
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
    /// The parts centred as the mod centres a blueprint (Blueprint.Center): on its centre piece when it has one (that
    /// piece then never made, the others turned by its turn taken back), else on the middle of their bounds on the
    /// ground (bounds that hold the file's origin too, as Unity's start from it) at 5 cm above the lowest.
    /// </summary>
    private static List<BlueprintPart> Centred(List<BlueprintPart> parts, string centerPiece)
    {
        if (parts.Count == 0)
        {
            return parts;
        }
        var min = NVector3.Min(NVector3.Zero, parts.Aggregate(parts[0].Position, (m, p) => NVector3.Min(m, p.Position)));
        var max = NVector3.Max(NVector3.Zero, parts.Aggregate(parts[0].Position, (m, p) => NVector3.Max(m, p.Position)));
        var center = new NVector3((min.X + max.X) * 0.5f, parts.Min(p => p.Position.Y) + 0.05f, (min.Z + max.Z) * 0.5f);
        var turn = Quaternion.Identity;
        int at = parts.FindIndex(p => p.Prefab == centerPiece);
        if (at >= 0)
        {
            center = parts[at].Position;
            turn = Quaternion.Inverse(parts[at].Rotation);
            parts[at] = parts[at] with { Chance = 0f };
        }
        // Unity's != on rotations: the same unless their dot product is below 0.999999.
        bool turned = Quaternion.Dot(turn, Quaternion.Identity) <= 0.999999f;
        return [.. parts.Select(p => turned
            ? p with { Position = NVector3.Transform(p.Position - center, turn), Rotation = turn * p.Rotation }
            : p with { Position = p.Position - center })];
    }
}
