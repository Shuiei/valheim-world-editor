using System.ComponentModel;
using ModelContextProtocol.Server;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// Claude's search of the game's objects and building pieces (find_prefabs, piece_info), and its guides (building_guide,
/// script_reference).
/// </summary>
public sealed partial class ClaudeTools
{
    /// <summary>find_prefabs: building pieces (with name, tool, size and cost) and other placeable objects, or items, by words of the name.</summary>
    [McpServerTool(Name = "find_prefabs", ReadOnly = true, Title = "Find what can be placed")]
    [Description("Searches the game's objects by name (prefab names such as Beech1, rock4_forest, woodwall, stone_floor_2x2): what can be placed in a world, and the building pieces with their English name, tool (hammer for building pieces), size and cost. Every word must match. Spawn points (kind SpawnPoints: invisible, each spawns its creature where it stands, once or again after it dies) and nests (kind Nests) are also found by the creature they spawn (\"boar\" finds Spawner_Boar), with what they spawn; place them with place_pieces.")]
    public static Task<string> FindPrefabs(
        [Description("Words of the name, e.g. \"stone wall\", \"pine\", \"roof 26\".")] string query,
        [Description("Only building pieces (the hammer's): true; anything: false.")] bool piecesOnly = false,
        [Description("At most this many.")] int limit = 60,
        [Description("Items instead (what goes in chests: Coins, Amber, IronScrap, weapons…): true.")] bool items = false) => OnUi(() =>
    {
        string[] words = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        bool Match(string s) => words.All(w => s.Contains(w, StringComparison.OrdinalIgnoreCase));
        if (items)
        {
            return ToJson(Terrain.PrefabCatalog.Items.Where(Match).Take(Math.Clamp(limit, 1, 500)).Select(i => new { item = i }));
        }
        var pieces = Terrain.PieceCatalog.Names
            .Select(n => (n, p: Terrain.PieceCatalog.Get(Save.StableHash.Of(n))))
            .Where(x => x.p is { Tool: not null } && (Match(x.n) || Match(Terrain.PieceCost.PieceName(x.n))))
            .Select(x => (object)new { prefab = x.n, name = Terrain.PieceCost.PieceName(x.n), tool = x.p!.Tool, size = SizeOf(x.p!), cost = Terrain.PieceCost.Of([x.n]).Describe(4, english: true) });
        var others = piecesOnly ? [] : Terrain.PrefabCatalog.Placeable
            .Where(i => Terrain.PieceCatalog.Get(Save.StableHash.Of(i.Name)) == null && (Match(i.Name) || Terrain.PrefabCatalog.SpawnOf(i.Name) is { } sp && Match(string.Join(" ", sp.Creatures))))
            .Select(i => Terrain.PrefabCatalog.SpawnOf(i.Name) is { } sp
                ? (object)new { prefab = i.Name, kind = ObjectKinds.Of(i.Name, false).ToString(), spawns = sp.Summary(english: true) }
                : new { prefab = i.Name, kind = ObjectKinds.Of(i.Name, false).ToString() });
        return ToJson(pieces.Concat(others).Take(Math.Clamp(limit, 1, 500)));
    });

    /// <summary>A piece's size (x, y, z) in metres, before turning.</summary>
    private static object SizeOf(Terrain.PieceCatalog.Info p) =>
        new { x = MathF.Round(p.MaxX - p.MinX, 2), y = MathF.Round(p.MaxY - p.MinY, 2), z = MathF.Round(p.MaxZ - p.MinZ, 2) };

    /// <summary>piece_info: name, tool, size, bounds, snap points, cost and station of building pieces.</summary>
    [McpServerTool(Name = "piece_info", ReadOnly = true, Title = "About building pieces")]
    [Description("For building pieces (prefab names): English name, tool, size and bounds from the piece's origin (x across, y up, z through, before turning), snap points (where pieces join, from the origin), cost and the station it needs. Use it to place pieces edge to edge.")]
    public static Task<string> PieceInfo([Description("Prefab names, e.g. [\"woodwall\", \"wood_floor\"].")] string[] prefabs) => OnUi(() => ToJson(prefabs.Select(n =>
    {
        var p = Terrain.PieceCatalog.Get(Save.StableHash.Of(n));
        if (p == null)
        {
            return (object)new { prefab = n, error = "not a building piece the editor knows (find_prefabs)" };
        }
        var cost = Terrain.PieceCost.Get(n);
        return new
        {
            prefab = n,
            name = Terrain.PieceCost.PieceName(n),
            tool = p.Tool,
            size = SizeOf(p),
            bounds = new { minX = p.MinX, maxX = p.MaxX, minY = p.MinY, maxY = p.MaxY, minZ = p.MinZ, maxZ = p.MaxZ },
            snaps = p.Snaps,
            cost = Terrain.PieceCost.Of([n]).Describe(8, english: true),
            station = cost?.Station,
        };
    })));

    /// <summary>building_guide: <see cref="ClaudeGuide.Building"/> and <see cref="ClaudeGuide.Shaping"/>.</summary>
    [McpServerTool(Name = "building_guide", ReadOnly = true, Title = "How to build and shape")]
    [Description("How Valheim's building works, from the game's data: the 2 m grid, pieces' origins and turns, roofs, and support (what holds and what falls); and how to shape ground. Read it before building.")]
    public static string BuildingGuide() => ClaudeGuide.Building + "\n\n" + ClaudeGuide.Shaping;

    /// <summary>script_reference: the script API's source.</summary>
    [McpServerTool(Name = "script_reference", ReadOnly = true, Title = "The script API")]
    [Description("The C# API run_script scripts use, as its source with comments: Area (bounds, points), Ground (Height, Set, Raise, Lower, Paint, Shape, Mountain, Biome), Objects (All, OfKind, Near, Place with tilts and snapping, Move, Remove, CanPlace), Noise, Rnd, Print. Read it before writing a script.")]
    public static string ScriptReference() => ScriptHost.ApiSource;
}
