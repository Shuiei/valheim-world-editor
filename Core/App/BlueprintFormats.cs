using System.Globalization;
using System.Numerics;
using System.Text;
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
public static class BlueprintFormats
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
    /// Reads a PlanBuild .blueprint or a .vbuild file (chosen by the file name's extension). Lines that cannot be read
    /// are counted, not fatal; other mods' sections are skipped. The editor's #EditorData lines are attached to their
    /// pieces, and a ruin's pieces get the no-builder field.
    /// </summary>
    /// <param name="fileName">The file's name or path: gives the format and the fallback name.</param>
    /// <param name="text">The file's content.</param>
    public static Parsed Parse(string fileName, string text)
    {
        string ext = Path.GetExtension(fileName).ToLowerInvariant();
        bool vbuild = ext == ".vbuild";
        string name = Path.GetFileNameWithoutExtension(fileName);
        string? creator = null, description = null;
        List<Piece> pieces = [];
        List<TerrainMod> terrain = [];
        int skipped = 0, line0 = 0;
        bool ruin = false;
        var data = new Dictionary<int, List<Editing.ObjectField>>();
        var lines = new List<int>();
        string section = "pieces";
        foreach (string raw in text.Split('\n'))
        {
            string line = raw.Trim().TrimStart('﻿');
            if (line.Length == 0)
            {
                continue;
            }
            if (line.StartsWith("#Name:", StringComparison.Ordinal)) { name = line[6..].Trim() is { Length: > 0 } n ? n : name; continue; }
            if (line.StartsWith("#Creator:", StringComparison.Ordinal)) { creator = line[9..].Trim(); continue; }
            if (line.StartsWith("#Description:", StringComparison.Ordinal)) { description = Unquote(line[13..].Trim()); continue; }
            if (line.StartsWith("#Category:", StringComparison.Ordinal)) { continue; }
            if (line == RuinHeader) { ruin = true; continue; }
            if (line.StartsWith(DataHeader, StringComparison.Ordinal))
            {
                string rest = line[DataHeader.Length..];
                int bar = rest.IndexOf(';');
                if (bar > 0 && int.TryParse(rest[..bar], NumberStyles.Integer, CultureInfo.InvariantCulture, out int at) && Editing.ObjectField.Parse(rest[(bar + 1)..]) is { } f)
                {
                    (data.TryGetValue(at, out var l) ? l : data[at] = []).Add(f);
                }
                continue;
            }
            if (line == "#SnapPoints") { section = "snap"; continue; }
            if (line == "#Terrain") { section = "terrain"; continue; }
            if (line == "#Pieces") { section = "pieces"; continue; }
            if (line.StartsWith('#'))
            {
                // Another mod's section (InfinityHammer's #TerrainHeight...): skipped like PlanBuild does.
                section = "skip";
                continue;
            }
            try
            {
                switch (section)
                {
                    case "terrain":
                        terrain.Add(ParseTerrain(line));
                        break;
                    case "pieces":
                        int n = line0++;
                        pieces.Add(vbuild ? ParseVBuild(line) : ParseBlueprintPiece(line));
                        lines.Add(n);
                        break;
                }
            }
            catch (Exception ex) when (ex is FormatException or IndexOutOfRangeException or OverflowException)
            {
                skipped++;
            }
        }
        if (data.Count > 0 || ruin)
        {
            for (int k = 0; k < pieces.Count; k++)
            {
                var d = data.TryGetValue(lines[k], out var held) ? held : [];
                if (ruin)
                {
                    d.Add(Editing.ObjectField.NoBuilder);
                }
                pieces[k] = pieces[k] with { Data = d };
            }
        }
        return new Parsed(name, creator, description, pieces, terrain, skipped, ruin);
    }

    /// <summary>
    /// One PlanBuild "#Pieces" line: name;category;x;y;z;qx;qy;qz;qw;info;sx;sy;sz (scale from sx when present).
    /// </summary>
    private static Piece ParseBlueprintPiece(string line)
    {
        // Old PlanBuild files were written with a comma as decimal separator.
        if (line.Contains(',') && !line.Contains('"'))
        {
            line = line.Replace(',', '.');
        }
        string[] p = line.Split(';');
        var q = Quaternion.Normalize(new Quaternion(F(p[5]), F(p[6]), F(p[7]), F(p[8])));
        float scale = p.Length > 12 ? F(p[10]) : 1f;
        return new Piece(PrefabName(p[0]), new Vector3(F(p[2]), F(p[3]), F(p[4])), ToEuler(q), scale);
    }

    /// <summary>
    /// One .vbuild line: name qx qy qz qw x y z (no scale).
    /// </summary>
    private static Piece ParseVBuild(string line)
    {
        if (line.Contains(','))
        {
            line = line.Replace(',', '.');
        }
        string[] p = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var q = Quaternion.Normalize(new Quaternion(F(p[1]), F(p[2]), F(p[3]), F(p[4])));
        return new Piece(PrefabName(p[0]), new Vector3(F(p[5]), F(p[6]), F(p[7])), ToEuler(q), 1f);
    }

    /// <summary>
    /// One PlanBuild "#Terrain" line: shape;x;y;z;radius;rotation;smooth;paint.
    /// </summary>
    private static TerrainMod ParseTerrain(string line)
    {
        string[] p = line.Split(';');
        return new TerrainMod(p[0].ToLowerInvariant(), new Vector3(F(p[1]), F(p[2]), F(p[3])), F(p[4]),
            (int)MathF.Round(F(p[5])), p.Length > 7 ? p[7] : "");
    }

    /// <summary>
    /// "piece_chest(Clone)" -&gt; "piece_chest", as PlanBuild does.
    /// </summary>
    private static string PrefabName(string s) => s.Split('(')[0].Trim();

    /// <summary>
    /// Strips the surrounding quotes PlanBuild writes around a description and undoes its \" and \n escapes.
    /// </summary>
    private static string Unquote(string s) => s.Length >= 2 && s[0] == '"' && s[^1] == '"' ? s[1..^1].Replace("\\\"", "\"").Replace("\\n", "\n") : s;

    /// <summary>
    /// A number in invariant culture; empty gives 0. One that is not finite ("NaN", "Infinity") is a FormatException,
    /// like text that is not a number: the line cannot be read.
    /// </summary>
    private static float F(string s)
    {
        if (string.IsNullOrWhiteSpace(s))
        {
            return 0f;
        }
        float v = float.Parse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture);
        return float.IsFinite(v) ? v : throw new FormatException($"{s.Trim()} is not a finite number");
    }

    /// <summary>
    /// A number as written to blueprint files: invariant culture, at most five decimals.
    /// </summary>
    private static string S(float f) => f.ToString("0.#####", CultureInfo.InvariantCulture);

    /// <summary>
    /// Unity's Euler angles (degrees, applied z, then x, then y: R = Ry * Rx * Rz) of a quaternion.
    /// </summary>
    public static Vector3 ToEuler(Quaternion q)
    {
        q = Quaternion.Normalize(q);
        float m02 = 2f * (q.X * q.Z + q.W * q.Y), m22 = 1f - 2f * (q.X * q.X + q.Y * q.Y);
        float m12 = 2f * (q.Y * q.Z - q.W * q.X), m10 = 2f * (q.X * q.Y + q.W * q.Z), m11 = 1f - 2f * (q.X * q.X + q.Z * q.Z);
        float m00 = 1f - 2f * (q.Y * q.Y + q.Z * q.Z), m20 = 2f * (q.X * q.Z - q.W * q.Y);
        float x, y, z;
        if (MathF.Abs(m12) < 0.99999f)
        {
            x = MathF.Asin(-m12);
            y = MathF.Atan2(m02, m22);
            z = MathF.Atan2(m10, m11);
        }
        else
        {
            // Looking straight up or down: only the sum of y and z is defined.
            x = m12 < 0 ? MathF.PI / 2 : -MathF.PI / 2;
            y = MathF.Atan2(-m20, m00);
            z = 0f;
        }
        return new Vector3(Deg(x), Deg(y), Deg(z));
        static float Deg(float r) { float d = r * 180f / MathF.PI; d %= 360f; return d < 0 ? d + 360f : d; }
    }

    /// <summary>
    /// Unity's Quaternion.Euler: z, then x, then y.
    /// </summary>
    public static Quaternion FromEuler(Vector3 e)
    {
        const float K = MathF.PI / 360f;
        Quaternion qx = new(MathF.Sin(e.X * K), 0, 0, MathF.Cos(e.X * K)), qy = new(0, MathF.Sin(e.Y * K), 0, MathF.Cos(e.Y * K)), qz = new(0, 0, MathF.Sin(e.Z * K), MathF.Cos(e.Z * K));
        // System.Numerics multiplies right to left like Unity: qy * qx * qz applies qz first.
        return Mul(Mul(qy, qx), qz);
    }

    /// <summary>
    /// Hamilton product a * b written out (a applied after b).
    /// </summary>
    private static Quaternion Mul(Quaternion a, Quaternion b) => new(
        a.W * b.X + a.X * b.W + a.Y * b.Z - a.Z * b.Y,
        a.W * b.Y - a.X * b.Z + a.Y * b.W + a.Z * b.X,
        a.W * b.Z + a.X * b.Y - a.Y * b.X + a.Z * b.W,
        a.W * b.W - a.X * b.X - a.Y * b.Y - a.Z * b.Z);

    /// <summary>
    /// The most metres the ground grid of a clip spans on a side: more than the editor's largest area (17 zones,
    /// 1088 m). A blueprint whose terrain entries reach further is refused rather than given a grid that does not
    /// fit in memory.
    /// </summary>
    public const int MaxGround = 1200;

    /// <summary>
    /// The editor's clipboard (as stored: heights in cm, -32768 = no ground; weights and paint in 1/255)
    /// from a parsed file. The lowest piece rests on the point where it is pasted; ground from terrain
    /// entries is levelled to their height, relative to the same point. The ground grid, centred on the
    /// same point, is as large as the terrain entries reach.
    /// </summary>
    /// <param name="bp">The parsed file.</param>
    /// <param name="known">Whether a prefab name can be placed; pieces it rejects are left out.</param>
    /// <param name="unknown">The rejected prefab names, distinct and sorted.</param>
    /// <exception cref="InvalidDataException">The terrain entries span more than <c>MaxGround</c> metres.</exception>
    public static JsonObject ToClip(Parsed bp, Func<string, bool> known, out List<string> unknown)
    {
        var pieces = bp.Pieces.Where(p => known(p.Name)).ToList();
        unknown = [.. bp.Pieces.Select(p => p.Name).Where(n => !known(n)).Distinct().OrderBy(n => n)];
        float minY = pieces.Count > 0 ? pieces.Min(p => p.Position.Y) : bp.Terrain.Count > 0 ? bp.Terrain.Min(t => t.Position.Y) : 0f;
        float x0 = float.MaxValue, x1 = float.MinValue, z0 = float.MaxValue, z1 = float.MinValue;
        void Grow(float x, float z) { x0 = Math.Min(x0, x); x1 = Math.Max(x1, x); z0 = Math.Min(z0, z); z1 = Math.Max(z1, z); }
        foreach (Piece p in pieces)
        {
            Grow(p.Position.X, p.Position.Z);
        }

        foreach (TerrainMod t in bp.Terrain) { Grow(t.Position.X - t.Radius, t.Position.Z - t.Radius); Grow(t.Position.X + t.Radius, t.Position.Z + t.Radius); }
        if (x0 > x1)
        {
            x0 = x1 = z0 = z1 = 0f;
        }
        float cx = (x0 + x1) / 2f, cz = (z0 + z1) / 2f;
        JsonArray objects = [];
        foreach (Piece p in pieces)
        {
            objects.Add(new JsonObject
            {
                ["name"] = p.Name,
                ["dx"] = R(p.Position.X - cx),
                ["dz"] = R(p.Position.Z - cz),
                ["dy"] = R(p.Position.Y - minY),
                ["rx"] = R(p.Euler.X),
                ["ry"] = R(p.Euler.Y),
                ["rz"] = R(p.Euler.Z),
                ["scale"] = MathF.Abs(p.Scale - 1f) < 1e-3f ? 0 : R(p.Scale),
                ["sourceId"] = null,
            });
            if (p.Data is { Count: > 0 } fields)
            {
                ((JsonObject)objects[^1]!)["data"] = DataJson(fields);
            }
        }
        int w = 1, h = 1;
        int[] rel = [-32768], wt = [0], pnt = [-255, -255, -255, -255];
        if (bp.Terrain.Count > 0)
        {
            // Only the ground the terrain entries reach is gridded, however far the pieces go, around the centre.
            float tx0 = float.MaxValue, tx1 = float.MinValue, tz0 = float.MaxValue, tz1 = float.MinValue;
            foreach (TerrainMod t in bp.Terrain)
            {
                tx0 = Math.Min(tx0, t.Position.X - t.Radius); tx1 = Math.Max(tx1, t.Position.X + t.Radius);
                tz0 = Math.Min(tz0, t.Position.Z - t.Radius); tz1 = Math.Max(tz1, t.Position.Z + t.Radius);
            }
            float halfW = Math.Max(cx - tx0, tx1 - cx), halfH = Math.Max(cz - tz0, tz1 - cz);
            if (halfW * 2 > MaxGround || halfH * 2 > MaxGround)
            {
                throw new InvalidDataException($"its ground marks span {halfW * 2:F0} m by {halfH * 2:F0} m, more than the editor's {MaxGround} m");
            }
            w = 2 * (int)MathF.Ceiling(halfW) + 1;
            h = 2 * (int)MathF.Ceiling(halfH) + 1;
            rel = [.. Enumerable.Repeat(-32768, w * h)];
            wt = new int[w * h];
            pnt = [.. Enumerable.Repeat(-255, w * h * 4)];
            for (int iz = 0; iz < h; iz++)
            {
                for (int ix = 0; ix < w; ix++)
                {
                    float x = cx + ix - (w - 1) / 2f, z = cz + iz - (h - 1) / 2f;
                    // Later entries are applied over earlier ones, as in game.
                    TerrainMod? hit = bp.Terrain.LastOrDefault(t => Covers(t, x, z));
                    if (hit == null)
                    {
                        continue;
                    }
                    int i = iz * w + ix;
                    rel[i] = (int)MathF.Round((hit.Position.Y - minY) * 100f);
                    wt[i] = 255;
                    float[]? paint = PaintOf(hit.Paint);
                    if (paint != null)
                    {
                        for (int c = 0; c < 4; c++)
                        {
                            pnt[i * 4 + c] = (int)MathF.Round(paint[c] * 255f);
                        }
                    }
                }
            }
        }
        const float Pad = 1f;
        JsonArray poly = new(
            Point(x0 - cx - Pad, z0 - cz - Pad), Point(x1 - cx + Pad, z0 - cz - Pad),
            Point(x1 - cx + Pad, z1 - cz + Pad), Point(x0 - cx - Pad, z1 - cz + Pad));
        return new JsonObject
        {
            ["w"] = w,
            ["h"] = h,
            ["rel"] = new JsonArray(rel.Select(v => (JsonNode)v).ToArray()),
            ["wt"] = new JsonArray(wt.Select(v => (JsonNode)v).ToArray()),
            ["pnt"] = new JsonArray(pnt.Select(v => (JsonNode)v).ToArray()),
            ["objects"] = objects,
            ["poly"] = poly,
            ["name"] = bp.Name,
        };
        static JsonObject Point(float gx, float gz) => new() { ["gx"] = R(gx), ["gz"] = R(gz) };
    }

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

    /// <summary>
    /// Whether a terrain entry covers the ground point (x, z): a circle, or a square turned by its rotation.
    /// </summary>
    private static bool Covers(TerrainMod t, float x, float z)
    {
        float dx = x - t.Position.X, dz = z - t.Position.Z;
        if (t.Shape != "square")
        {
            return dx * dx + dz * dz <= t.Radius * t.Radius;
        }
        // A square turned by the entry's rotation (Unity yaw: clockwise seen from above).
        float a = -t.Rotation * MathF.PI / 180f, c = MathF.Cos(a), s = MathF.Sin(a);
        float lx = dx * c + dz * s, lz = -dx * s + dz * c;
        return MathF.Abs(lx) <= t.Radius && MathF.Abs(lz) <= t.Radius;
    }

    /// <summary>
    /// PlanBuild's paint names -&gt; the game's paint mask (r dirt, g cultivated, b paved, a vegetation).
    /// </summary>
    private static float[]? PaintOf(string paint) => paint.ToLowerInvariant() switch
    {
        var p when p.Contains("dirt") => [1f, 0f, 0f, 1f],
        var p when p.Contains("cultiv") => [0f, 1f, 0f, 1f],
        var p when p.Contains("paved") => [0f, 0f, 1f, 1f],
        _ => null,
    };

    /// <summary>
    /// The objects of a clipboard (blueprint file content) as a PlanBuild .blueprint or a .vbuild file.
    /// Ground is not written: PlanBuild's terrain entries cannot describe free-form ground.
    /// </summary>
    /// <param name="clip">The clipboard: its "objects" are written.</param>
    /// <param name="format">"vbuild" for a .vbuild file, anything else for a PlanBuild .blueprint.</param>
    /// <param name="name">The blueprint's #Name.</param>
    /// <param name="category">A prefab's piece category index (0 Misc, 1 Crafting...); out of range gives Misc.</param>
    public static string Write(JsonObject clip, string format, string name, Func<string, int> category)
    {
        var objs = (clip["objects"] as JsonArray ?? []).OfType<JsonObject>().Select(o => (
            Name: (string?)o["name"] ?? "", X: (float?)o["dx"] ?? 0, Y: (float?)o["dy"] ?? 0, Z: (float?)o["dz"] ?? 0,
            Rot: FromEuler(new Vector3((float?)o["rx"] ?? 0, (float?)o["ry"] ?? 0, (float?)o["rz"] ?? 0)), Scale: (float?)o["scale"] ?? 0)).ToList();
        // PlanBuild measures from the lowest corner; ground level stays where the copy had it.
        float minX = objs.Count > 0 ? objs.Min(o => o.X) : 0, minZ = objs.Count > 0 ? objs.Min(o => o.Z) : 0;
        StringBuilder sb = new();
        if (format == "vbuild")
        {
            foreach (var (prefab, x, y, z, rot, scale) in objs.OrderBy(o => o.Y))
            {
                sb.Append(prefab).Append(' ').Append(S(rot.X)).Append(' ').Append(S(rot.Y)).Append(' ').Append(S(rot.Z)).Append(' ').Append(S(rot.W))
                    .Append(' ').Append(S(x - minX)).Append(' ').Append(S(y)).Append(' ').Append(S(z - minZ)).Append('\n');
            }
            return sb.ToString();
        }
        string[] categories = ["Misc", "Crafting", "BuildingWorkbench", "BuildingStonecutter", "Furniture", "DeepNorth", "Feasts", "Food", "Meads"];
        sb.Append("#Name:").Append(name).Append('\n');
        sb.Append("#Creator:Valheim World Editor\n");
        sb.Append("#Description:\"\"\n");
        sb.Append("#Category:Misc\n");
        sb.Append("#Pieces\n");
        foreach (var (prefab, x, y, z, rot, scale) in objs.OrderBy(o => o.Y).ThenBy(o => o.X).ThenBy(o => o.Z))
        {
            int cat = category(prefab);
            float s = scale > 0 ? scale : 1f;
            sb.Append(string.Join(";", prefab, cat >= 0 && cat < categories.Length ? categories[cat] : "Misc",
                S(x - minX), S(y), S(z - minZ), S(rot.X), S(rot.Y), S(rot.Z), S(rot.W), "\"\"", S(s), S(s), S(s))).Append('\n');
        }
        return sb.ToString();
    }
}
