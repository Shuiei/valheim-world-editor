using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.Json.Nodes;

namespace TerrainEditor.App;

/// <summary>
/// Writing a copy as a Homestead blueprint file: its header lines, its pieces and the editor's own data lines.
/// </summary>
public static partial class Homestead
{
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
        var ordered = Pieces(clip);
        float radius = ordered.Count == 0 ? 0 : MathF.Sqrt(ordered.Max(o => o.X * o.X + o.Z * o.Z)) + 1f;
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
        DataLines(sb, ordered);
        sb.Append("#Pieces\n");
        foreach (var (prefab, x, y, z, rot, scale, _) in ordered)
        {
            float s = scale > 0 ? scale : 1f;
            sb.Append(string.Join(";", PieceName(prefab), "Building", S(x), S(y), S(z),
                S(rot.X), S(rot.Y), S(rot.Z), S(rot.W), "\"\"", S(s), S(s), S(s))).Append('\n');
        }
        return sb.ToString();
    }

    /// <summary>
    /// An object of a copy as the blueprint writers take it (Homestead's and PlanBuild's).
    /// </summary>
    /// <param name="Name">Its prefab name.</param>
    /// <param name="X">Metres from the copy's middle along x.</param>
    /// <param name="Y">Metres from the copy's ground.</param>
    /// <param name="Z">Metres from the copy's middle along z.</param>
    /// <param name="Rot">Its rotation.</param>
    /// <param name="Scale">Its scale; 0 when the copy has none.</param>
    /// <param name="Data">What it holds (a chest's contents, a sign's text...).</param>
    internal readonly record struct Piece(string Name, float X, float Y, float Z, Quaternion Rot, float Scale, List<Editing.ObjectField> Data);

    /// <summary>
    /// A copy's objects for a blueprint file, ordered by height then x then z (a data line's number is its piece's place
    /// in this order); objects without a prefab name are left out.
    /// </summary>
    internal static List<Piece> Pieces(JsonObject clip) =>
    [
        .. (clip["objects"] as JsonArray ?? []).OfType<JsonObject>()
            .Select(o => new Piece((string?)o["name"] ?? "", F(o["dx"]), F(o["dy"]), F(o["dz"]),
                BlueprintFormats.FromEuler(new Vector3(F(o["rx"]), F(o["ry"]), F(o["rz"]))), F(o["scale"]), BlueprintFormats.DataOf(o["data"]) ?? []))
            .Where(o => o.Name.Length > 0).OrderBy(o => o.Y).ThenBy(o => o.X).ThenBy(o => o.Z),
    ];

    /// <summary>
    /// The editor's own header lines: what the pieces hold, each by its number in <paramref name="pieces"/>, one line
    /// for all when every piece is a ruin's (no builder) instead of one each, and the marks of the pieces only the
    /// editor can place (<see cref="HammerPlaces"/>, worked out from the game's data here, whatever a file read in
    /// said), after the header that says the file carries them.
    /// </summary>
    internal static void DataLines(StringBuilder sb, List<Piece> pieces)
    {
        bool ruin = pieces.Count > 0 && pieces.All(o => o.Data.Contains(Editing.ObjectField.NoBuilder));
        if (ruin)
        {
            sb.Append(BlueprintFormats.RuinHeader).Append('\n');
        }
        sb.Append(BlueprintFormats.MarkedHeader).Append('\n');
        for (int k = 0; k < pieces.Count; k++)
        {
            if (!HammerPlaces(pieces[k].Name))
            {
                sb.Append(BlueprintFormats.DataHeader).Append(k.ToString(CultureInfo.InvariantCulture)).Append(';').Append(BlueprintFormats.OnlyMark).Append('\n');
            }
            foreach (var f in pieces[k].Data.Where(f => !(ruin && f == Editing.ObjectField.NoBuilder)))
            {
                sb.Append(BlueprintFormats.DataHeader).Append(k.ToString(CultureInfo.InvariantCulture)).Append(';').Append(f.ToString()).Append('\n');
            }
        }
    }

    /// <summary>A prefab name as a piece line takes it: one line, without the field separator.</summary>
    internal static string PieceName(string prefab) => Header(prefab).Replace(";", "", StringComparison.Ordinal);

    /// <summary>
    /// A JSON number as a float, 0 when missing; a number written as a string ("1.5") is read too. Read in place, not
    /// through its JSON text, but for numbers no float holds as they are (FormatException for what is no number).
    /// </summary>
    private static float F(JsonNode? n) => n switch
    {
        null => 0,
        JsonValue v when v.TryGetValue(out float f) => f,
        JsonValue v when v.TryGetValue(out string? text) => float.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture),
        _ => float.Parse(n.ToJsonString(), CultureInfo.InvariantCulture),
    };

    /// <summary>Homestead writes numbers with three decimals at most.</summary>
    private static string S(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);

    /// <summary>A value made one header line: line breaks become spaces, trimmed.</summary>
    internal static string Header(string v) => v.Replace('\r', ' ').Replace('\n', ' ').Trim();
}
