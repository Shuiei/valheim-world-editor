using System.ComponentModel;
using ModelContextProtocol.Server;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class ClaudeTools
{
    /// <summary>The most hits search_world gives.</summary>
    private const int MostHits = 500;

    /// <summary>search_world: WorldSearch over the open world, with the open area's id of each hit inside it.</summary>
    [McpServerTool(Name = "search_world", ReadOnly = true, Title = "Search the whole world")]
    [Description("Searches every object of the open world, as the map's Search does (also where no area is open): by kind (what: kinds; e.g. \"portal\", \"Beech\", \"piece_chest\"), by item in containers (what: items; e.g. \"Coins\", \"Surtling\"), or by text (what: texts; signs, portal tags, ward and tombstone names). Gives how many were found by name and where each is (world x, y, z; nearest a point first when x, z are given), with its id in the open area (areaId) when it is in it. Open the area around a hit (open_area) to work on it.")]
    public async Task<string> SearchWorld(
        [Description("What to look for: part of a name, an item or a text (case does not matter).")] string query,
        [Description("kinds (objects by name), items (in containers) or texts (default kinds).")] string what = "kinds",
        [Description("World x of a point: nearest hits first (optional, with z).")] float? x = null,
        [Description("World z of that point.")] float? z = null,
        [Description("At most this many hits (default 50, up to 500).")] int limit = 50)
    {
        if (what is not ("kinds" or "items" or "texts"))
        {
            return "what is kinds, items or texts.";
        }
        if (LoneCoordinate(x, z) is string lone)
        {
            return lone;
        }
        // The open area and how often its world was read, to tell after the search whether its ids still fit.
        var (world, scene, reads) = await OnUi(() =>
        {
            var open = _w.InWorkshop ? null : _w.Session?.Scene;
            return (_w.World, open, open?.Reads ?? 0);
        });
        if (world == null)
        {
            return "Open a world first (open_world).";
        }
        WorldSearch.Result res;
        try
        {
            res = await Task.Run(() => SearchFiles(world.World, world.Edits, query, what));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Caught(ex, "searching the world's files for Claude");
            return "The search failed: " + ex.Message;
        }
        return await OnUi(() => SearchAnswer(world, scene, reads, res, x, z, limit));
    }
}
