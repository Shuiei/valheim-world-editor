using System.ComponentModel;
using System.Globalization;
using ModelContextProtocol.Server;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class ClaudeTools
{
    /// <summary>get_object: everything an object is and holds, as the inspector shows it.</summary>
    [McpServerTool(Name = "get_object", ReadOnly = true, Title = "Everything about an object")]
    [Description("Everything an object of the open area is and holds, as the editor's inspector shows it: name, kind, position, turn (yaw, pitch, roll), size, whether a player built it, a container's contents (item, stack, quality, slot) and every value it keeps in the save by name (a sign's text, a portal's tag, health, fuel, builder (creator, creatorName), owner, a ward's permitted players, a creature's level and tamed state, timers…). Change values with set_object_data, contents with set_contents.")]
    public Task<string> GetObject([Description("The object's id (list_objects).")] int id) => OnUi(() =>
    {
        if (_w.Session is not { } s)
        {
            return "No area is open.";
        }
        int reads = s.Scene.Reads;
        if (ObjectProblem(s.Scene, id) is string refused)
        {
            return refused;
        }
        GaveIds(s.Scene, reads);
        var t = s.Scene.Things[id];
        var head = HeadOf(id, t);
        if (t.Runestone)
        {
            head["note"] = "A runestone holds no values of its own: the game builds it from its location.";
            return ToJson(head);
        }
        if (Values(s, t) is not { } values)
        {
            head["note"] = "Its values cannot be read.";
            return ToJson(head);
        }
        head["values"] = DataOf(values);
        if (Terrain.PrefabCatalog.Details(t.Prefab) is { ContainerW: > 0 } box)
        {
            head["slots"] = new { columns = box.ContainerW, rows = box.ContainerH };
        }
        if (values.FirstOrDefault(v => v.Section == "bytes" && v.Key == ObjectData.ItemsKey).Value is string items)
        {
            head["contents"] = ContentsOf(items);
        }
        return ToJson(head);
    });

    /// <summary>An object's head as get_object gives it first: its id, names, kind, place, turn, size and whether a player built it.</summary>
    /// <param name="id">The object's id.</param>
    /// <param name="t">The object.</param>
    internal Dictionary<string, object?> HeadOf(int id, WorldScene.Thing t) => new()
    {
        ["id"] = id,
        ["name"] = _w.NameOfPrefab(t.Prefab),
        ["displayName"] = Terrain.PrefabCatalog.DisplayName(t.Prefab),
        ["kind"] = KindOf(t),
        ["x"] = MathF.Round(t.Position.X, 3),
        ["y"] = MathF.Round(t.Position.Y, 3),
        ["z"] = MathF.Round(t.Position.Z, 3),
        ["yaw"] = MathF.Round(t.Rotation.Y, 2),
        ["pitch"] = MathF.Round(t.Rotation.X, 2),
        ["roll"] = MathF.Round(t.Rotation.Z, 2),
        ["scale"] = t.Scale > 0 ? MathF.Round(t.Scale, 3) : 1,
        ["built"] = t.Piece,
    };

    /// <summary>A container's contents as get_object gives them, from its saved items; "cannot be read" when they are damaged.</summary>
    /// <param name="items">The items' saved bytes, as base64 text.</param>
    private static object ContentsOf(string items)
    {
        try
        {
            return Save.InventoryData.Read(Convert.FromBase64String(items)).Items.Select(i => new
            {
                item = Terrain.PrefabCatalog.NameOf(i.Prefab) ?? i.Prefab.ToString(CultureInfo.InvariantCulture),
                stack = i.Stack,
                quality = i.Quality,
                variant = i.Variant,
                slot = new[] { i.X, i.Y },
            });
        }
        catch (Exception ex) when (Save.CheckedReads.Damaged(ex))
        {
            Log.Caught(ex, "reading a container's contents for Claude");
            return "cannot be read";
        }
    }
}
