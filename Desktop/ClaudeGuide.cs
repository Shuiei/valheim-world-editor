using System.ComponentModel;
using ModelContextProtocol.Server;

namespace TerrainEditor.Desktop;

// What Claude is told about working in the editor: the instructions sent when it connects (the
// workflow, coordinates, what it may and may not do), the building guide (Valheim's pieces and support,
// from the game's data), and prompts to start from.
public static class ClaudeGuide
{
	public const string Instructions = """
		You are connected to Valheim World Editor, running on the user's computer. You change the world or
		the Workshop open in it, as the user would, and the user watches in the 3D view.

		Work like this: editor_state first; open what is needed (open_world, open_area, open_workshop);
		look (describe_area, list_objects, screenshot, area_map); change in small steps; after each step,
		look again (screenshot, area_map; support_check for buildings) and fix what is wrong.

		Coordinates are the game's, in metres: x east, z north, y up. The world is made of 64 m zones; an
		area opened in 3D is 1 to 5 zones across (editor_state gives its bounds, and nothing can be put
		outside them). The Workshop is a blank 192 m plot whose ground is at y = 34, its middle at x = 0, z = 0.

		Every change is one step of the history, labelled "Claude: ...", and stays pending: you never save a
		world, apply anything to a live game or save a blueprint; the user does, or takes your changes back
		(Ctrl+Z, or History > Take back Claude's changes). Opening another world over unsaved changes is refused:
		ask the user. While the world is live with Auto on, you can change nothing: ask the user to turn
		Auto off.

		To shape ground or place many objects, prefer the ready tools (flatten, paint_area, road, forest)
		or run_script (read script_reference first; dryRun shows what a script would change). To build,
		prefer build_floor, build_walls and build_roof, then place_pieces for details; read building_guide
		first. Ask the user before changing things they did not ask for.
		""";

	public const string Building = """
		Building in Valheim (from the game's data; piece_info gives any piece's exact size and snap points).

		Grid and origins
		- Wood pieces are on a 2 m grid: woodwall is 2 x 2 m, wood_floor 2 x 2 m, wood_wall_half 2 x 1 m.
		  Stone: stone_wall_2x1 (2 x 1 m, 1 m thick), stone_wall_4x2 (4 x 2 m), stone_floor_2x2 (2 x 2 m,
		  1 m thick).
		- A wall's origin is its middle (woodwall reaches 1 m below and above it); a floor's origin is its
		  top surface. A wall standing on a floor whose top is at y: its origin at y + 1.
		- Yaw turns round the vertical: 0 = the piece's front faces north (+z), 90 east, 180 south, 270 west.
		  A wall along x (an east-west wall) has yaw 0; along z, yaw 90.
		- Pieces snap: put a piece where it should join (within 0.5 m) and its snap points meet those of
		  the pieces next to it, as the game's hammer does.

		Roofs (thatch: wood_roof, wood_roof_45; shingle: darkwood_roof, darkwood_roof_45)
		- A slope piece covers 2 m across and 2 m of run; it rises 1 m (26 degrees) or 2 m (45 degrees).
		  Its low edge is toward its local +z: yaw 0 sheds water north, 180 south, 90 east, 270 west.
		- The ridge (wood_roof_top, wood_roof_top_45, darkwood_roof_top...) joins the two slopes' high edges,
		  1 m either side of its middle.
		- Gable ends are filled with sloped walls (wood_wall_roof: a 2 m base rising 1 m; wood_wall_roof_45:
		  rising 2 m) over full or half walls. build_roof does all of this.

		Support (what holds, as the game computes it; support_check shows it)
		- A piece touching the ground holds fully. Support passes to the pieces it touches and drops with
		  distance: wood loses 20 % per metre sideways and 12.5 % per metre up, and falls below 10 % of 100.
		  Core wood (hardwood) and timber reach further; iron much further.
		- Stone loses all its support within 1 m sideways: stone walls and floors need stone (or the ground)
		  right under them; stone cannot overhang. Black marble and grausten span further.
		- Long wooden spans need posts (wood_pole2, wood_pole_log_4) or beams (wood_beam, wood_beam_26) down to
		  the ground every few metres; high roofs need beams under them.
		- support_check names what would fall: add posts or walls under it, or move it closer to support.
		""";

	public const string Shaping = """
		Shaping an area. The game lets ground move at most 8 m from its original height (scripts:
		Ground.NoLimit = true goes further, through locations the game understands). Water is at y = 30;
		ground below it is under water. Paint kinds: dirt, cultivated, paved, clear (the biome's own). Kinds
		of objects (list_objects, Objects.OfKind): Trees, Rocks, Ore, Bushes, Pickables, Buildings...
		Trees and rocks are placed on the ground with Objects.Place or forest; find_prefabs finds their
		names (Beech1, Birch1, Oak1, FirTree, Pinetree_01, rock4_forest...). Look at the result with
		area_map (heights and objects from above, with coordinates) and screenshot.
		""";
}

// The prompts Claude's clients offer to start from (registered with WithPrompts).
[McpServerPromptType]
public sealed class ClaudePrompts
{
	[McpServerPrompt(Name = "build_in_workshop", Title = "Build in the Workshop")]
	[Description("Build something in the Workshop from a description, then check that it stands.")]
	public static string BuildInWorkshop([Description("What to build, e.g. \"a small longhouse with a stone floor and a thatch roof\".")] string what) =>
		$"""
		In Valheim World Editor, build this in the Workshop: {what}

		{ClaudeGuide.Building}

		Steps: open_workshop (ask me first if it has unsaved work); plan the size on the 2 m grid; build_floor,
		build_walls (with doors), build_roof; details with place_pieces; then support_check and screenshot
		from a few sides, and fix what would fall or looks wrong. Tell me when it is ready: I save the blueprint.
		""";

	[McpServerPrompt(Name = "shape_area", Title = "Shape an area")]
	[Description("Change the ground and objects of an area of the open world from a description.")]
	public static string ShapeArea([Description("What to make, e.g. \"a valley with a river running north, birches along it\".")] string what) =>
		$"""
		In Valheim World Editor, change the area of the open world: {what}

		{ClaudeGuide.Shaping}

		Steps: editor_state; open_area where it should be (ask me where if it is unclear); describe_area and
		area_map; make it in steps (flatten, road, paint_area, forest, or run_script; dryRun first for large
		scripts), looking at area_map and screenshot after each. Tell me when it is ready: I save it.
		""";
}
