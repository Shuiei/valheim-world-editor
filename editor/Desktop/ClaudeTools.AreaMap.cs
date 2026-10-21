using System.ComponentModel;
using ModelContextProtocol.Server;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// Claude's map of the open area (area_map): a top-down picture drawn from its data, with its bounds in words.
/// </summary>
public sealed partial class ClaudeTools
{
    /// <summary>area_map: a top-down map of the open area drawn from its data (see <see cref="DrawMap"/>), with its bounds in words.</summary>
    [McpServerTool(Name = "area_map", ReadOnly = true, Title = "A map of the area with coordinates")]
    [Description("A top-down map of the open area (or the plot), drawn from its data, north up: ground heights in colour with hill shading, painted ground (paved grey, dirt brown, cultivated dark), water in blue, objects as dots (trees green, rocks grey, buildings orange, others white), and a grid labelled in world metres. Use it to read exact places, plan, and check ground work.")]
    public async Task<ModelContextProtocol.Protocol.CallToolResult> AreaMap(
        [Description("Grid spacing in metres (default 16).")] float grid = 16,
        [Description("Draw the objects (default true).")] bool objects = true)
    {
        var data = await OnUi(() =>
        {
            if (_w.Session?.Scene is not { } scene)
            {
                return null;
            }
            // Only what is needed is taken here (the window's thread): the objects' kinds are worked out after.
            List<(float X, float Z, int Prefab, bool Piece, bool Tamed)> things;
            lock (scene.Things)
            {
                things = [.. scene.Things.Where(t => !t.Gone).Select(t => (t.Position.X, t.Position.Z, t.Prefab, t.Piece, t.Tamed))];
            }
            var names = new Dictionary<int, string?>();
            foreach (int prefab in things.Select(t => t.Prefab))
            {
                if (!names.ContainsKey(prefab))
                {
                    names[prefab] = _w.NameOfPrefab(prefab);
                }
            }
            // Paint (dirt, cultivated, paved) per ground point, as the ground has it now (Ground.MaskOf).
            var g = _w.Session.Ground;
            float[]? paint = null;
            if (g.W == scene.W && g.H == scene.H)
            {
                paint = new float[scene.W * scene.H * 3];
                for (int p = 0; p < scene.W * scene.H; p++)
                {
                    float[] from = g.PMod[p] != 0 ? g.Paint : g.BaseMask;
                    paint[p * 3] = from[p * 4];
                    paint[p * 3 + 1] = from[p * 4 + 1];
                    paint[p * 3 + 2] = from[p * 4 + 2];
                }
            }
            return new { scene.W, scene.H, scene.Cx, scene.Cz, scene.Water, Heights = (float[])scene.Heights.Clone(), Paint = paint, Things = things, Names = names };
        });
        if (data == null)
        {
            return new() { IsError = true, Content = { new ModelContextProtocol.Protocol.TextContentBlock { Text = "No area is open." } } };
        }
        byte[] jpeg = await Task.Run(() =>
        {
            List<(float X, float Z, string Kind)> things = objects
                ? [.. data.Things.Select(t => (t.X, t.Z, ObjectKinds.Of(data.Names[t.Prefab], t.Piece, t.Tamed).ToString()))]
                : [];
            return DrawMap(data.W, data.H, data.Cx, data.Cz, data.Water, data.Heights, data.Paint, things, MathF.Max(4, grid));
        });
        float minX = data.Cx - (data.W - 1) / 2f, minZ = data.Cz - (data.H - 1) / 2f;
        return new()
        {
            Content =
            {
                new ModelContextProtocol.Protocol.TextContentBlock { Text = $"x {minX:0} to {minX + data.W - 1:0} (left to right), z {minZ:0} to {minZ + data.H - 1:0} (bottom to top); heights {data.Heights.Min():0.0} to {data.Heights.Max():0.0} m, water at {data.Water:0.0} m." },
                ModelContextProtocol.Protocol.ImageContentBlock.FromBytes(jpeg, "image/jpeg"),
            },
        };
    }
}
