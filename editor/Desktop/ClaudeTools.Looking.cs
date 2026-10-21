using System.ComponentModel;
using ModelContextProtocol.Server;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// Claude's tools for looking at the editor: its state, the worlds on the start page, opening a world or an area, and what
/// the open area holds (describe_area, and the kind of each object).
/// </summary>
public sealed partial class ClaudeTools
{
    /// <summary>editor_state: the page shown, the world, the area's bounds, what is pending (see <see cref="StateOf"/>).</summary>
    [McpServerTool(Name = "editor_state", ReadOnly = true, Title = "What the editor shows")]
    [Description("What the editor shows now: the start page, a world's map, an area of a world in 3D, or the Workshop (a blank plot to build a blueprint on); the world, the area's bounds in world coordinates, the changes not saved yet, and the editor's last message. Call it first.")]
    public Task<string> EditorState() => OnUi(() => ToJson(StateOf(_w)));

    /// <summary>list_worlds: the start page's worlds.</summary>
    [McpServerTool(Name = "list_worlds", ReadOnly = true, Title = "Worlds on this computer")]
    [Description("The Valheim worlds on this computer (the start page's list): name, folder, last save. Open one with open_world.")]
    public Task<string> ListWorlds() => OnUi(() => ToJson(Worlds.Find(_w.Settings)
        .Select(i => new { name = i.Name, folder = i.Path, saved = i.Saved, where = i.Where, usable = i.Usable, problem = i.Problem })));

    /// <summary>open_world: opens a world offline, refused while there are unsaved changes.</summary>
    [McpServerTool(Name = "open_world", Title = "Open a world")]
    [Description("Opens a world from its folder (from list_worlds), offline, and shows its map. Refused while the open world or the Workshop has changes not saved: ask the user to save or discard them first.")]
    public Task<string> OpenWorld([Description("The world's folder, as list_worlds gives it.")] string folder) => OnUi(async () =>
    {
        if (Unsaved() is string why)
        {
            return why;
        }
        await _w.OpenWorld(() => Task.Run(() => WorldSession.Open(folder)), "Opening the world for Claude…");
        return _w.World != null ? ToJson(StateOf(_w)) : "Could not open it: " + _w.LastMessageEn;
    });

    /// <summary>open_area: opens the zones around a world point in 3D (size clamped to 1 to 17 zones).</summary>
    [McpServerTool(Name = "open_area", Title = "Open an area in 3D")]
    [Description("Opens the square area around a point of the open world in the 3D editor, where it can be looked at and changed. The world is made of 64 m zones; size is how many zones across (1 to 17; 3 = 192 m is usual; large areas are slow to open and draw). Changes made in another area stay pending.")]
    public Task<string> OpenArea(
        [Description("World x (east) of the area's middle, in metres.")] float x,
        [Description("World z (north) of the area's middle, in metres.")] float z,
        [Description("Zones across: 1 to 17.")] int size = 3) => OnUi(async () =>
    {
        if (_w.World == null)
        {
            return "Open a world first (open_world).";
        }
        var (zx, zz) = Save.ChunkMath.SectorOf(x, z);
        await _w.EditArea(zx, zz, Math.Clamp(size, 1, MainWindow.MaxFollowZones));
        return ToJson(StateOf(_w));
    });

    /// <summary>describe_area: the open area's bounds, heights, biomes and objects counted by kind and by name (the top 40).</summary>
    [McpServerTool(Name = "describe_area", ReadOnly = true, Title = "Describe the open area")]
    [Description("The open area (or the Workshop's plot): its bounds, ground heights (lowest, highest, average; the water level), its biomes, and its objects counted by kind and by name.")]
    public Task<string> DescribeArea() => OnUi(() =>
    {
        if (_w.Session?.Scene is not { } scene)
        {
            return "No area is open (open_area, or the Workshop).";
        }
        float[] h = scene.Heights;
        var biomes = scene.Biomes.GroupBy(b => ((ValheimGen.Heightmap.Biome)b).ToString()).OrderByDescending(g => g.Count())
            .Select(g => new { biome = g.Key, share = MathF.Round(100f * g.Count() / scene.Biomes.Length, 1) });
        List<(string Name, string Kind)> things;
        lock (scene.Things)
        {
            things = [.. scene.Things.Where(t => !t.Gone).Select(t => (Name: _w.NameOfPrefab(t.Prefab) ?? t.Prefab.ToString(), Kind: KindOf(t)))];
        }
        return ToJson(new
        {
            area = StateOf(_w),
            ground = new { lowest = h.Min(), highest = h.Max(), average = MathF.Round(h.Average(), 2), water = scene.Water },
            biomes,
            objectsByKind = things.GroupBy(t => t.Kind).OrderByDescending(g => g.Count()).Select(g => new { kind = g.Key, count = g.Count() }),
            objectsByName = things.GroupBy(t => t.Name).OrderByDescending(g => g.Count()).Take(40).Select(g => new { name = g.Key, count = g.Count() }),
        });
    });

    /// <summary>
    /// The refusal when only one of a point's two coordinates is given (null: both or neither): the other was dropped
    /// or mixed with a default without a word.
    /// </summary>
    /// <param name="x">The point's x, if given.</param>
    /// <param name="z">The point's z, if given.</param>
    /// <param name="xName">The x argument's name.</param>
    /// <param name="zName">The z argument's name.</param>
    internal static string? LoneCoordinate(float? x, float? z, string xName = "x", string zName = "z") =>
        x.HasValue == z.HasValue ? null : $"Give {xName} and {zName} together, or neither: only {(x.HasValue ? xName : zName)} was given.";

    /// <summary>A thing's object kind, as describe_area and list_objects name it.</summary>
    private string KindOf(WorldScene.Thing t) => ObjectKinds.Of(_w.NameOfPrefab(t.Prefab), t.Piece, t.Tamed).ToString();
}
