using System.Text.Json.Nodes;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;
/// <summary>
/// A copy as JSON, the web editor's clipboard format (editor/area.js encodeClip): heights in cm
/// (-32768 = no ground), edge weights and paint in 1/255, objects and outline as they are. Blueprint
/// files hold it, so the two editors read each other's blueprints. And a small picture of a copy.
/// </summary>
public static partial class CopyFormat
{
    /// <summary>A number rounded to four decimals, for the JSON.</summary>
    private static double R(float v) => Math.Round(v, 4);

    /// <summary>
    /// A relative height in whole centimetres, as large as it is (a copy off a tall No-limit mountain goes past the web
    /// editor's ±327.67 m; every reader takes any whole number); only -32768, the "no ground" marker, is moved a
    /// centimetre.
    /// </summary>
    private static int Centimetres(float v)
    {
        int cm = (int)MathF.Round(v * 100);
        return cm == -32768 ? -32767 : cm;
    }

    /// <summary>A copy in the clipboard format.</summary>
    public static JsonObject ToJson(CopyData c) => new()
    {
        ["w"] = c.W,
        ["h"] = c.H,
        ["rel"] = new JsonArray(c.Rel.Select(v => (JsonNode)(float.IsNaN(v) ? -32768 : Centimetres(v))).ToArray()),
        ["wt"] = new JsonArray(c.Wt.Select(v => (JsonNode)(int)MathF.Round(v * 255)).ToArray()),
        ["pnt"] = new JsonArray(c.Pnt.Select(v => (JsonNode)(int)MathF.Round(v * 255)).ToArray()),
        ["objects"] = new JsonArray(c.Objects.Select(o => (JsonNode)new JsonObject
        {
            ["name"] = o.Name,
            ["dx"] = R(o.Dx),
            ["dz"] = R(o.Dz),
            ["dy"] = R(o.Dy),
            ["rx"] = R(o.Rotation.X),
            ["ry"] = R(o.Rotation.Y),
            ["rz"] = R(o.Rotation.Z),
            ["scale"] = R(o.Scale),
            ["sourceId"] = o.SourceId,
            ["follow"] = o.Follow,
            ["data"] = o.Data is { Count: > 0 } d ? BlueprintFormats.DataJson(d) : null,
        }).ToArray()),
        ["poly"] = new JsonArray(c.Poly.Select(p => (JsonNode)new JsonObject { ["gx"] = R(p.X), ["gz"] = R(p.Y) }).ToArray()),
        ["name"] = c.Name,
    };

    /// <summary>Numbers as written by either editor (whole or not, float or double inside the node); d when missing.</summary>
    private static float F(JsonNode? n, float d = 0) => n == null ? d : float.Parse(n.ToJsonString(), System.Globalization.CultureInfo.InvariantCulture);
    /// <summary>A number as written by either editor, rounded to a whole one (0 when missing).</summary>
    private static int I(JsonNode? n) => (int)MathF.Round(F(n));
}
