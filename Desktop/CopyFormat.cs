using System.Numerics;
using System.Text.Json.Nodes;
using SkiaSharp;
using TerrainEditor.App;
using TerrainEditor.Save;

namespace TerrainEditor.Desktop;

/// <summary>
/// A copy as JSON, the web editor's clipboard format (editor/area.js encodeClip): heights in cm
/// (-32768 = no ground), edge weights and paint in 1/255, objects and outline as they are. Blueprint
/// files hold it, so the two editors read each other's blueprints. And a small picture of a copy.
/// </summary>
public static class CopyFormat
{
    /// <summary>A number rounded to four decimals, for the JSON.</summary>
    private static double R(float v) => Math.Round(v, 4);

    /// <summary>A copy in the clipboard format.</summary>
    public static JsonObject ToJson(CopyData c) => new()
    {
        ["w"] = c.W,
        ["h"] = c.H,
        ["rel"] = new JsonArray(c.Rel.Select(v => (JsonNode)(float.IsNaN(v) ? -32768 : (int)Math.Clamp(MathF.Round(v * 100), -32767, 32767))).ToArray()),
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

    /// <summary>A copy from the clipboard format.</summary>
    /// <param name="o">The JSON.</param>
    /// <param name="name">Its name; null: the one in the JSON.</param>
    /// <param name="keepSources">Keep the objects' source ids: they only mean something in the world the copy came from.</param>
    /// <exception cref="FormatException">
    /// The copy's size is not positive, its ground arrays are missing or not as long as its size says (W × H heights and
    /// weights, four paint values a point), an object has no name or a point of its outline is not one: pasted, it would
    /// stop half-way through.
    /// </exception>
    public static CopyData FromJson(JsonObject o, string? name = null, bool keepSources = true)
    {
        int w = I(o["w"]), h = I(o["h"]);
        if (w <= 0 || h <= 0)
        {
            throw new FormatException($"its size ({w} × {h}) is not a grid");
        }
        long points = (long)w * h;
        var rel = Numbers(o, "rel", points, w, h);
        var wt = Numbers(o, "wt", points, w, h);
        var pnt = Numbers(o, "pnt", points * 4, w, h);
        if (o["objects"] is not JsonArray objects)
        {
            throw new FormatException("it has no objects list");
        }
        if (objects.Any(x => x is not JsonObject ob || ob["name"] is not JsonValue v || !v.TryGetValue(out string? n) || string.IsNullOrWhiteSpace(n)))
        {
            throw new FormatException("an object in it has no name");
        }
        if (o["poly"] is JsonArray outline && outline.Any(p => p is not JsonObject))
        {
            throw new FormatException("a point of its outline is not a point");
        }
        return new()
        {
            W = w,
            H = h,
            Rel = [.. rel.Select(v => I(v) == -32768 ? float.NaN : I(v) / 100f)],
            Wt = [.. wt.Select(v => I(v) / 255f)],
            Pnt = [.. pnt.Select(v => I(v) < 0 ? -1f : I(v) / 255f)],
            Objects = [.. objects.Select(x => new CopyData.Obj(StableHash.OfName((string)x!["name"]!), (string)x["name"]!, F(x["dx"]), F(x["dz"]), F(x["dy"]),
                new Vector3(F(x["rx"]), F(x["ry"]), F(x["rz"])), F(x["scale"]), keepSources && x["sourceId"] != null ? I(x["sourceId"]) : null, (bool?)x["follow"] ?? false,
                BlueprintFormats.DataOf(x["data"])))],
            Poly = (o["poly"] as JsonArray)?.Select(p => new Vector2(F(p!["gx"]), F(p["gz"]))).ToList() ?? [],
            Name = name ?? (string?)o["name"],
        };
    }

    /// <summary>A copy's ground array, checked to hold as many values as its size says.</summary>
    /// <param name="o">The copy's JSON.</param>
    /// <param name="key">The array's key.</param>
    /// <param name="count">How many values it must hold.</param>
    /// <param name="w">The copy's width, for the message.</param>
    /// <param name="h">The copy's depth, for the message.</param>
    /// <exception cref="FormatException">It is missing or of another length.</exception>
    private static JsonArray Numbers(JsonObject o, string key, long count, int w, int h) =>
        o[key] is not JsonArray a ? throw new FormatException($"it has no \"{key}\" list")
        : a.Count != count ? throw new FormatException($"its \"{key}\" list holds {a.Count} values, not the {count} a {w} × {h} copy needs")
        : a;

    /// <summary>
    /// A small picture of a copy seen from above (north up): the ground shaded by height, objects as dots
    /// coloured by kind (the web editor's drawThumb), as a PNG data URL.
    /// </summary>
    /// <param name="c">The copy.</param>
    /// <param name="size">The picture's width and height in pixels.</param>
    public static string Thumb(CopyData c, int size = 160)
    {
        using var bmp = new SKBitmap(size, size);
        using var g = new SKCanvas(bmp);
        g.Clear(new SKColor(0x1e, 0x23, 0x2b));
        float x0 = float.MaxValue, x1 = float.MinValue, z0 = float.MaxValue, z1 = float.MinValue;
        void Grow(float x, float z) { x0 = MathF.Min(x0, x); x1 = MathF.Max(x1, x); z0 = MathF.Min(z0, z); z1 = MathF.Max(z1, z); }
        foreach (var p in c.Poly)
        {
            Grow(p.X, p.Y);
        }

        foreach (var o in c.Objects)
        {
            Grow(o.Dx, o.Dz);
        }

        bool ground = c.Rel.Any(v => !float.IsNaN(v));
        if (ground)
        {
            Grow(-(c.W - 1) / 2f, -(c.H - 1) / 2f);
            Grow((c.W - 1) / 2f, (c.H - 1) / 2f);
        }
        if (x0 <= x1)
        {
            float span = MathF.Max(4, MathF.Max(x1 - x0, z1 - z0)) * 1.1f, mx = (x0 + x1) / 2, mz = (z0 + z1) / 2;
            float Sx(float x) => (x - mx) / span * size + size / 2f;
            float Sy(float z) => size / 2f - (z - mz) / span * size;
            if (ground)
            {
                float lo = c.Rel.Where(v => !float.IsNaN(v)).Min(), hi = c.Rel.Where(v => !float.IsNaN(v)).Max(), cell = MathF.Max(1, size / span);
                using var paint = new SKPaint();
                for (int iz = 0; iz < c.H; iz++)
                {
                    for (int ix = 0; ix < c.W; ix++)
                    {
                        float v = c.Rel[iz * c.W + ix];
                        if (float.IsNaN(v))
                        {
                            continue;
                        }
                        float t = hi > lo ? (v - lo) / (hi - lo) : 0.5f;
                        bool painted = c.Pnt[(iz * c.W + ix) * 4] >= 0;
                        paint.Color = painted ? new SKColor((byte)(150 + 40 * t), (byte)(120 + 30 * t), (byte)(80 + 20 * t)) : new SKColor((byte)(70 + 110 * t), (byte)(110 + 70 * t), (byte)(55 + 60 * t));
                        g.DrawRect(Sx(ix - (c.W - 1) / 2f) - cell / 2, Sy(iz - (c.H - 1) / 2f) - cell / 2, cell + 0.5f, cell + 0.5f, paint);
                    }
                }
            }
            if (c.Poly.Count > 0)
            {
                using var line = new SKPaint { Color = new SKColor(95, 212, 255, 204), StrokeWidth = 1.5f, Style = SKPaintStyle.Stroke, IsAntialias = true };
                using var path = new SKPath();
                path.MoveTo(Sx(c.Poly[0].X), Sy(c.Poly[0].Y));
                foreach (var p in c.Poly.Skip(1))
                {
                    path.LineTo(Sx(p.X), Sy(p.Y));
                }
                path.Close();
                g.DrawPath(path, line);
            }
            float r = Math.Clamp(size / span * 0.6f, 1.5f, 4);
            using var dot = new SKPaint { IsAntialias = true };
            foreach (var o in c.Objects)
            {
                dot.Color = ObjectKinds.Of(o.Name, false) switch
                {
                    ObjectKind.Ruins => SKColor.Parse("#a08cc8"),
                    ObjectKind.Trees => SKColor.Parse("#2f6b2a"),
                    ObjectKind.Rocks => SKColor.Parse("#9a9a9a"),
                    ObjectKind.Ore => SKColor.Parse("#e0803a"),
                    ObjectKind.Bushes => SKColor.Parse("#7cbf5a"),
                    ObjectKind.Pickables => SKColor.Parse("#e8d24a"),
                    _ => SKColor.Parse("#e6e9ee"),
                };
                g.DrawCircle(Sx(o.Dx), Sy(o.Dz), r, dot);
            }
        }
        using var img = SKImage.FromBitmap(bmp);
        using var data = img.Encode(SKEncodedImageFormat.Png, 90);
        return "data:image/png;base64," + Convert.ToBase64String(data.ToArray());
    }
}
