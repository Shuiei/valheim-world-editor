using System.Numerics;
using System.Text.Json.Nodes;

namespace TerrainEditor.App;

/// <summary>
/// Blueprint files of other Valheim mods, read into the editor's clipboard format and written from it:
///   PlanBuild .blueprint   "#Name:", "#Creator:", "#Description:", "#Category:", optional "#SnapPoints" and
///                          "#Terrain" sections, then "#Pieces": name;category;x;y;z;qx;qy;qz;qw;info;sx;sy;sz
///   .vbuild (BuildShare)   one piece per line: name qx qy qz qw x y z
/// Positions are relative to the blueprint's origin along the world axes; rotations are Unity
/// quaternions. Terrain entries (shape;x;y;z;radius;rotation;smooth;paint) level and paint the ground.
/// </summary>
public static partial class BlueprintFormats
{
    /// <summary>
    /// One piece of a blueprint.
    /// </summary>
    /// <param name="Name">The prefab name (any "(Clone)" suffix dropped).</param>
    /// <param name="Position">Relative to the blueprint's origin, along the world axes (metres).</param>
    /// <param name="Euler">The rotation as Unity Euler angles in degrees (see <c>ToEuler</c>).</param>
    /// <param name="Scale">Uniform scale, 1 for none.</param>
    /// <param name="Data">Values of the object's data the editor keeps for it (its own #EditorData lines).</param>
    public sealed record Piece(string Name, Vector3 Position, Vector3 Euler, float Scale, IReadOnlyList<Editing.ObjectField>? Data = null);

    /// <summary>
    /// A PlanBuild #Terrain entry: ground levelled to the entry's height and painted.
    /// </summary>
    /// <param name="Shape">The shape in lower case: "square", anything else is a circle.</param>
    /// <param name="Position">The centre, relative to the blueprint's origin; Y is the height the ground is levelled
    /// to.</param>
    /// <param name="Radius">The circle's radius, or half the square's side (metres).</param>
    /// <param name="Rotation">The square's yaw in degrees (Unity: clockwise seen from above).</param>
    /// <param name="Paint">PlanBuild's paint name ("dirt", "cultivated", "paved"...; empty: none).</param>
    public sealed record TerrainMod(string Shape, Vector3 Position, float Radius, int Rotation, string Paint);

    /// <summary>
    /// A blueprint file as read by <c>Parse</c>.
    /// </summary>
    /// <param name="Name">The #Name header, or the file name without extension.</param>
    /// <param name="Creator">The #Creator header, if any.</param>
    /// <param name="Description">The #Description header, unquoted, if any.</param>
    /// <param name="Pieces">The pieces, in file order.</param>
    /// <param name="Terrain">The #Terrain entries, in file order.</param>
    /// <param name="SkippedLines">Piece or terrain lines that could not be read.</param>
    /// <param name="Ruin">Written by the editor as a ruin (no builder; #EditorRuin), as generated dungeons are.</param>
    public sealed record Parsed(string Name, string? Creator, string? Description, List<Piece> Pieces, List<TerrainMod> Terrain, int SkippedLines, bool Ruin = false);

    /// <summary>
    /// The editor's own header lines (other mods skip header lines they do not know): an object's data,
    /// "#EditorData:n;section;key;value" (n: the object's line among the pieces, from 0), and a building
    /// written as a ruin. Homestead and PlanBuild build without them.
    /// </summary>
    public const string DataHeader = "#EditorData:", RuinHeader = "#EditorRuin:1";

    /// <summary>
    /// The most metres the ground grid of a clip spans on a side: more than the editor's largest area (17 zones,
    /// 1088 m). A blueprint whose terrain entries reach further is refused rather than given a grid that does not
    /// fit in memory.
    /// </summary>
    public const int MaxGround = 1200;

    /// <summary>
    /// Rounds to a thousandth, to keep the clipboard JSON short.
    /// </summary>
    private static float R(float v) => MathF.Round(v * 1000f) / 1000f;

    /// <summary>
    /// An object's data in the clipboard format: its fields as text (ObjectField).
    /// </summary>
    public static JsonArray DataJson(IEnumerable<Editing.ObjectField> fields) => [.. fields.Select(f => (JsonNode)f.ToString()).ToArray()];

    /// <summary>
    /// The reverse of <c>DataJson</c>: an object's fields from the clipboard's "data" array, null when there are none.
    /// </summary>
    public static List<Editing.ObjectField>? DataOf(JsonNode? data) =>
        data is JsonArray a && a.Count > 0 ? [.. a.Select(n => Editing.ObjectField.Parse((string?)n ?? "")).OfType<Editing.ObjectField>()] : null;
}
