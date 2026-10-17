using System.Numerics;
using System.Text.Json.Nodes;
using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using TerrainEditor.Terrain;

namespace TerrainEditor.Desktop;

public static partial class Workshop
{
    /// <summary>
    /// What is on the plot as a copy (the clipboard format, for BlueprintsPanel): every object standing
    /// (the plot starts bare: all on it was put there), measured from the anchor, with what it holds (a
    /// chest's contents, a sign's text, a creature's stars...).
    /// </summary>
    /// <param name="s">The Workshop scene.</param>
    /// <param name="name">The copy's name.</param>
    /// <param name="lift">How far the blueprint opened was lifted (its anchor that much below the ground).</param>
    public static JsonObject Building(WorldScene s, string name, float lift = 0)
    {
        var anchor = Anchor(s) + new Vector3(0, lift, 0);
        var objects = new JsonArray();
        lock (s.Things)
        {
            foreach (var t in s.Things.Where(t => !t.Gone && PrefabCatalog.Details(t.Prefab) != null))
            {
                var o = new JsonObject
                {
                    ["name"] = PrefabCatalog.NameOf(t.Prefab),
                    ["dx"] = R(t.Position.X - anchor.X),
                    ["dz"] = R(t.Position.Z - anchor.Z),
                    ["dy"] = R(t.Position.Y - anchor.Y),
                    ["rx"] = R(t.Rotation.X),
                    ["ry"] = R(t.Rotation.Y),
                    ["rz"] = R(t.Rotation.Z),
                    ["scale"] = R(t.Scale),
                    ["sourceId"] = null,
                };
                var data = DataOf(s, t);
                if (data.Count > 0)
                {
                    o["data"] = BlueprintFormats.DataJson(data);
                }
                objects.Add(o);
            }
        }
        float half = objects.Count == 0 ? 1 : objects.OfType<JsonObject>().Max(o => MathF.Max(MathF.Abs((float)o["dx"]!), MathF.Abs((float)o["dz"]!))) + 2;
        var clip = BlueprintFormats.OneCellClip(objects, new JsonArray(P(-half, -half), P(half, -half), P(half, half), P(-half, half)));
        clip["name"] = name;
        return clip;
        static JsonObject P(float x, float z) => new() { ["gx"] = x, ["gz"] = z };
    }

    /// <summary>
    /// What an object of the plot holds: every value of its data but its builder and scale (a blueprint
    /// has its own), and no builder at all on a ruin's: the plot opened from one, or a building piece
    /// without a builder (added from one: the others got the chosen builder when they were put).
    /// </summary>
    private static List<ObjectField> DataOf(WorldScene s, WorldScene.Thing t)
    {
        var list = new List<ObjectField>();
        bool ruin = s.Ruin;
        if (s.Session is { } session && ObjectData.Bytes(s.World, session.Edits, t.Id) is { } bytes)
        {
            ruin |= WorldSave.Builder != 0 && PieceCatalog.Get(t.Prefab)?.Tool != null && !ZdoData.Parse(bytes).LongList.Any(f => f.Key == ObjectField.CreatorKey);
            list.AddRange(ObjectData.Fields(bytes, builder: false));
        }
        if (ruin)
        {
            list.Add(ObjectField.NoBuilder);
        }
        return list;
    }
}
