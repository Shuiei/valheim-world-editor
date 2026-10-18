using System.ComponentModel;
using ModelContextProtocol.Server;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// Claude's dungeon generation (generate_dungeon), placed on the Workshop's plot or above a point of the open world.
/// </summary>
public sealed partial class ClaudeTools
{
    /// <summary>
    /// generate_dungeon: a dungeon from the Dungeon panel's settings (clamped to their ranges), onto the Workshop's plot
    /// or 5000 m above a point of the open world, behind the entrance of its kind there (a portal pair when it has none).
    /// </summary>
    [McpServerTool(Name = "generate_dungeon", Title = "Generate a dungeon")]
    [Description("Generates a dungeon, as the Dungeon panel does, and puts it on the Workshop's plot (with what is there) or, in a world, 5000 m above the point x, z (where the game keeps its dungeons), behind the entrance of its kind on the ground there (its biome's dungeon location: a burial chamber's mound, a Frost Cave's mouth; a linked portal pair when the biome has none). The view stays at the entrance. One step of the history (pending).")]
    public async Task<string> GenerateDungeon(
        [Description("Its biome: Meadows, Black Forest, Swamp, Mountain, Plains, Mistlands, Ashlands, Deep North.")] string biome = "Black Forest",
        [Description("Its style (optional: the biome's): Crypt, Catacombs, Temple, Fortress, Prison, Dvergr hold, Goblin warren, Ruins.")] string? style = null,
        [Description("Its wall material (optional: the biome's).")] string? walls = null,
        [Description("Rooms per level: 1 small to 4 huge.")] int size = 2,
        [Description("Levels, 1 to 16.")] int levels = 2,
        [Description("Monsters: 0 none to 3 (1 usual).")] float monsters = 1,
        [Description("Chests and treasure: 0 none to 2 (1 usual).")] float loot = 1,
        [Description("Light: 0 dark, 1 dim, 2 bright.")] int light = 1,
        [Description("Furniture: 0 bare, 1 furnished, 2 rich.")] int decor = 1,
        [Description("Any number: each makes another dungeon with the same choices.")] int seed = 1,
        [Description("Its name, on a sign at the entrance (optional: made up).")] string? name = null,
        [Description("In a world: x of the point of the open area it goes above (optional: the view's middle).")] float? x = null,
        [Description("In a world: z of that point.")] float? z = null)
    {
        var settings = new Editing.DungeonGen.Settings(Biome: biome, Style: style, Walls: walls, Size: Math.Clamp(size, 1, 4), Levels: Math.Clamp(levels, 1, Editing.DungeonGen.MaxLevels),
            Monsters: Math.Clamp(monsters, 0, 3), Loot: Math.Clamp(loot, 0, 2), Light: Math.Clamp(light, 0, 2), Decor: Math.Clamp(decor, 0, 2), Seed: seed, Name: name);
        var (why, opened) = await OnUi(() => (CannotEdit(), _w.Session));
        if (why != null)
        {
            return why;
        }
        var result = await Task.Run(() => Editing.DungeonGen.Make(settings));
        return await WithCamera(async () =>
        {
            // The map shown or another area opened while it was made: it goes nowhere.
            if (_w.Session != opened || opened == null)
            {
                return "The area was closed (or another opened) while the dungeon was made: nothing was placed.";
            }
            // Or Auto turned on meanwhile: what forbade editing before the dungeon was made forbids placing it.
            if (CannotEdit() is string problem)
            {
                return problem;
            }
            // One of Claude's steps ("Claude:", as Take back Claude's changes finds them), and no file in the user's
            // blueprint Library.
            string label = ClaudesStep(Lang.T("claude.undo.dungeon", result.Name));
            if (_w.InWorkshop)
            {
                _w.AddGeneratedToPlot(settings, result, label);
            }
            else
            {
                if (x is float px && z is float pz)
                {
                    // Above a point of the open area (its ground is the area's).
                    var sc = opened.Scene;
                    var (minX, minZ, maxX, maxZ) = Bounds(sc);
                    if (px < minX || pz < minZ || px > maxX || pz > maxZ)
                    {
                        return $"({px}, {pz}) is outside the open area (x {minX} to {maxX}, z {minZ} to {maxZ}): open_area there first.";
                    }
                    _w.View.Orbit(px, pz, 45, 55, 120);
                }
                _w.PlaceGenerated(settings, result, label);
            }
            return $"{Said.Join(" ", result.Notes).English} {_w.LastMessageEn} Now pending: {Pending()}";
        });
    }
}
