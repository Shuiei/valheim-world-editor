using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;

namespace TerrainEditor.App;

/// <summary>
/// Writing a clipboard back out as a PlanBuild .blueprint or a .vbuild file.
/// </summary>
public static partial class BlueprintFormats
{
    /// <summary>
    /// A number as written to blueprint files: invariant culture, at most five decimals.
    /// </summary>
    private static string S(float f) => f.ToString("0.#####", CultureInfo.InvariantCulture);

    /// <summary>
    /// The objects of a clipboard (blueprint file content) as a PlanBuild .blueprint or a .vbuild file. The .blueprint
    /// also gets the editor's lines for what objects hold (as Homestead's, read back when opened again); a .vbuild has
    /// no place for them. Ground is not written: PlanBuild's terrain entries cannot describe free-form ground.
    /// </summary>
    /// <param name="clip">The clipboard: its "objects" are written.</param>
    /// <param name="format">"vbuild" for a .vbuild file, anything else for a PlanBuild .blueprint.</param>
    /// <param name="name">The blueprint's #Name.</param>
    /// <param name="category">A prefab's piece category index (0 Misc, 1 Crafting...); out of range gives Misc.</param>
    public static string Write(JsonObject clip, string format, string name, Func<string, int> category)
    {
        var objs = Homestead.Pieces(clip);
        // PlanBuild measures from the lowest corner; ground level stays where the copy had it.
        float minX = objs.Count > 0 ? objs.Min(o => o.X) : 0, minZ = objs.Count > 0 ? objs.Min(o => o.Z) : 0;
        StringBuilder sb = new();
        if (format == "vbuild")
        {
            foreach (var (prefab, x, y, z, rot, _, _) in objs)
            {
                sb.Append(prefab).Append(' ').Append(S(rot.X)).Append(' ').Append(S(rot.Y)).Append(' ').Append(S(rot.Z)).Append(' ').Append(S(rot.W))
                    .Append(' ').Append(S(x - minX)).Append(' ').Append(S(y)).Append(' ').Append(S(z - minZ)).Append('\n');
            }
            return sb.ToString();
        }
        string[] categories = ["Misc", "Crafting", "BuildingWorkbench", "BuildingStonecutter", "Furniture", "DeepNorth", "Feasts", "Food", "Meads"];
        sb.Append("#Name:").Append(Homestead.Header(name)).Append('\n');
        sb.Append("#Creator:Valheim World Editor\n");
        sb.Append("#Description:\"\"\n");
        sb.Append("#Category:Misc\n");
        // The editor's own lines (what the pieces hold), read back when the file is opened again.
        Homestead.DataLines(sb, objs);
        sb.Append("#Pieces\n");
        foreach (var (prefab, x, y, z, rot, scale, _) in objs)
        {
            int cat = category(prefab);
            float s = scale > 0 ? scale : 1f;
            sb.Append(string.Join(";", Homestead.PieceName(prefab), cat >= 0 && cat < categories.Length ? categories[cat] : "Misc",
                S(x - minX), S(y), S(z - minZ), S(rot.X), S(rot.Y), S(rot.Z), S(rot.W), "\"\"", S(s), S(s), S(s))).Append('\n');
        }
        return sb.ToString();
    }
}
