using System.ComponentModel;
using ModelContextProtocol.Server;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// Claude's building in larger pieces (build_floor, build_walls, build_roof), laid out over a rectangle by ClaudeBuilder.
/// </summary>
public sealed partial class ClaudeTools
{
    /// <summary>A door for build_walls.</summary>
    public sealed class DoorSpec
    {
        /// <summary>Which wall: north, south, east or west.</summary>
        [Description("Which wall: north, south, east or west.")]
        public string Side { get; set; } = "south";

        /// <summary>Where along it, in world metres: x on a north or south wall, z on an east or west one.</summary>
        [Description("Where along it: x for a north or south wall, z for an east or west one (a 2 m door at the 2 m of wall holding that point).")]
        public float At { get; set; }
    }

    /// <summary>
    /// Puts down a layout worked out from the open scene over a rectangle; refused when the rectangle goes outside the
    /// area, and a layout's <see cref="ArgumentException"/> (too many pieces among them) is the answer.
    /// </summary>
    /// <param name="x0">One corner, x.</param>
    /// <param name="z0">One corner, z.</param>
    /// <param name="x1">The opposite corner, x.</param>
    /// <param name="z1">The opposite corner, z.</param>
    /// <param name="layout">The pieces, from the open scene.</param>
    /// <param name="label">A few words for the history.</param>
    private Task<string> Build(float x0, float z0, float x1, float z1, Func<WorldScene, List<PieceSpec>> layout, string label) => OnUi(() =>
    {
        if (CannotEdit() is string why)
        {
            return why;
        }
        var (minX, minZ, maxX, maxZ) = Bounds(_w.Session!.Scene);
        // Written so that a coordinate that is not a number is outside too.
        if (!(MathF.Min(x0, x1) >= minX && MathF.Max(x0, x1) <= maxX && MathF.Min(z0, z1) >= minZ && MathF.Max(z0, z1) <= maxZ))
        {
            return $"Nothing built: the rectangle (x {x0} to {x1}, z {z0} to {z1}) goes outside the area (x {minX} to {maxX}, z {minZ} to {maxZ}). Open a larger area (open_area) or build within it.";
        }
        try
        {
            return Place(layout(_w.Session!.Scene), label);
        }
        catch (ArgumentException ex)
        {
            Log.CaughtBrief(ex, "building what Claude asked for");
            return ex.Message;
        }
    });

    /// <summary>build_floor: <see cref="ClaudeBuilder.Floor"/>, its top on the highest ground under it by default.</summary>
    [McpServerTool(Name = "build_floor", Title = "Build a floor")]
    [Description("Lays a floor of 2 x 2 m tiles (wood or stone) over a rectangle, its top at y (default: the highest ground under it, so that it rests on the ground). One pending step; the rectangle must be within the open area, and at most 5000 pieces are laid at once. Sizes that are not multiples of 2 m overhang a little.")]
    public Task<string> BuildFloor(
        [Description("West edge (x).")] float x0, [Description("South edge (z).")] float z0,
        [Description("East edge (x).")] float x1, [Description("North edge (z).")] float z1,
        [Description("wood or stone.")] string material = "wood",
        [Description("Height of its top (optional).")] float? y = null) =>
        Build(x0, z0, x1, z1, scene =>
        {
            // Too many tiles is refused before the ground under them is read.
            ClaudeBuilder.CheckFloor(x0, z0, x1, z1);
            return ClaudeBuilder.Floor(x0, z0, x1, z1, y ?? HighestGround(scene, x0, z0, x1, z1), material);
        }, $"floor of {material}");

    /// <summary>
    /// build_walls: <see cref="ClaudeBuilder.Walls"/>, from the top of a floor in the rectangle by default, else the
    /// highest ground on its edges.
    /// </summary>
    [McpServerTool(Name = "build_walls", Title = "Build walls")]
    [Description("Builds walls on the four edges of a rectangle (the walls' middle lines on its edges), from the bottom up to a height: wood (2 m rows) or stone (1 m rows); doors are 2 m wood doors in the walls at the given points. Bottom: the top of a floor already in the rectangle, else the highest ground on its edges. One pending step; the rectangle must be within the open area, and at most 5000 pieces are laid at once. Use the floor's own rectangle so that they meet.")]
    public Task<string> BuildWalls(
        [Description("West edge (x).")] float x0, [Description("South edge (z).")] float z0,
        [Description("East edge (x).")] float x1, [Description("North edge (z).")] float z1,
        [Description("Height, metres (2 or 4 for one storey).")] float height = 4,
        [Description("wood or stone.")] string material = "wood",
        [Description("Doors (optional).")] DoorSpec[]? doors = null,
        [Description("Height of the walls' bottom (optional).")] float? y = null) =>
        Build(x0, z0, x1, z1, scene =>
        {
            // Too many pieces is refused before the ground under them is read.
            ClaudeBuilder.CheckWalls(x0, z0, x1, z1, height, material);
            return ClaudeBuilder.Walls(x0, z0, x1, z1, y ?? HighestPieceTop(scene, x0, z0, x1, z1, "floor") ?? HighestGround(scene, x0, z0, x1, z1, edgesOnly: true),
                height, material, [.. (doors ?? []).Select(d => new ClaudeBuilder.Door(d.Side, d.At))]);
        }, $"walls of {material}");

    /// <summary>build_roof: <see cref="ClaudeBuilder.Roof"/>, its eave at the top of the pieces in the rectangle by default.</summary>
    [McpServerTool(Name = "build_roof", Title = "Build a roof")]
    [Description("Builds a gable roof over a rectangle (the walls' lines), its ridge along the longer side: thatch or shingle, 26 or 45 degrees, its underside meeting the wall lines at the eave height (default: the top of the building pieces in the rectangle, i.e. the walls), overhanging up to 2 m; the two gable ends closed with sloped walls (gables: true). One pending step; the rectangle must be within the open area, and at most 5000 pieces are laid at once. Check it with support_check and screenshot (views: 4).")]
    public Task<string> BuildRoof(
        [Description("West edge (x).")] float x0, [Description("South edge (z).")] float z0,
        [Description("East edge (x).")] float x1, [Description("North edge (z).")] float z1,
        [Description("26 or 45 degrees.")] int angle = 45,
        [Description("thatch or shingle.")] string material = "thatch",
        [Description("Close the gable ends with sloped walls (default true).")] bool gables = true,
        [Description("Height of the walls' tops (optional).")] float? eave = null) =>
        Build(x0, z0, x1, z1, scene => ClaudeBuilder.Roof(x0, z0, x1, z1,
            eave ?? HighestPieceTop(scene, x0, z0, x1, z1) ?? throw new ArgumentException("No walls under the roof: build the walls first, or give eave."),
            angle, material, gables), $"{material} roof");
}
