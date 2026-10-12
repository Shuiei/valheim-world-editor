# Claude

The editor can let **Claude** (in Claude Code, or any other MCP client) work on the world or the
Workshop you have open: look at it, shape the ground, plant forests, lay roads, build, generate
dungeons. You describe what you want in a prompt; Claude does it in the editor, step by step, and you
watch it happen in the 3D view.

Whatever Claude changes stays **pending**, like your own changes: one step of the history each
(Ctrl+Z takes it back), shown in the save bar. In **History**, Claude's steps carry a *Claude* tag,
and **Take back Claude's changes** removes all of them while keeping yours, even those you made after. Claude never saves a world, never applies anything to a
live game and never saves a blueprint: you do, when you like what you see.

## Turning it on

1. **Settings** (on the start page) → **Claude** → **Allow Claude to connect**.
2. **Save**. The editor now answers on `http://127.0.0.1:5731/mcp`, on this computer only.
3. Copy the command shown under the switch and run it once in a terminal. It adds the editor to
   Claude Code:

   ```sh
   claude mcp add --transport http valheim-editor http://127.0.0.1:5731/mcp --header "Authorization: Bearer <token>"
   ```

**Claude Desktop** starts a program rather than connecting to an address: Settings shows the lines
to add to its configuration (Claude Desktop → Settings → Developer → Edit Config,
`claude_desktop_config.json`), then restart it:

```json
{
  "mcpServers": {
    "valheim-editor": { "command": "/path/to/ValheimWorldEditor", "args": ["--mcp-stdio"] }
  }
}
```

`--mcp-stdio` opens no window: it passes Claude's messages to the editor that is running (which must
have **Allow Claude to connect** on), with the token from its settings.

Other MCP clients connect to the address (streamable HTTP) with the same `Authorization` header.

The token is a secret made for your editor: only a program that sends it is answered, so other
programs on the computer and web pages you visit cannot reach the editor. **New token** makes another
one (add the editor to Claude again with the new command). The port can be changed if 5731 is taken.

The editor must be running for Claude to reach it. Turn the switch off and nothing can connect.

## What Claude can do

| Looking | |
|---|---|
| `editor_state` | What is open: the start page, a world's map, an area in 3D or the Workshop; the area's bounds; what is pending |
| `list_worlds`, `open_world` | The worlds on this computer; open one (offline). Refused while the open world has changes not saved |
| `preview_seed`, `create_world` | What a seed's world will be like (a map of its biomes; where the start, traders and bosses will be), as the New world page shows it; make a new world of a name and seed and open it |
| `open_area` | Open the area around a point in 3D (1 to 17 zones of 64 m across) |
| `describe_area`, `list_objects` | Heights, biomes and objects of the area; objects by kind, name or place, with their ids |
| `ground_at` | The ground's exact height at points (now and as generated), its biome and any water there |
| `screenshot` | A picture of the 3D view (from any camera, or four views of a place in one picture) or of the map, so Claude sees what it did |
| `area_map` | A map of the area from above, drawn from its data: heights, water, painted ground, objects, a grid in world metres |
| `get_selection`, `select_objects` | What you selected in the editor ("these trees"); Claude selecting objects to show you which it means |
| `list_controls`, `set_control` | Every control of the editor (View switches, the Workshop's Cut and Support check, each tool's settings, the rail of tools, buttons): listed with their values, and set as you would. Saving, applying live, discarding, reloading, writing or deleting your files and connecting to the live game stay yours |
| `building_guide` | How Valheim's pieces fit and hold (the 2 m grid, roofs, support), from the game's data |
| `find_prefabs`, `piece_info` | The game's objects and building pieces: names, sizes, snap points, cost; spawn points and nests with what they spawn |
| `list_blueprints` | Your blueprint library |
| `search_world` | Every object of the world by kind, items in containers or texts (signs, portal tags…), as the map's Search |
| `list_locations` | The world's locations: the start, traders, bosses' altars, dungeon entrances, villages…, with what players call them |

| Changing | |
|---|---|
| `run_script` | A C# script on the area, as the [Script tool](scripting.md) runs it (`script_reference` gives Claude the API): ground, paint, objects; a dry run says what it would change |
| `flatten`, `paint_area`, `road`, `forest` | Level a rectangle, paint ground, lay a road through points, plant trees or rocks |
| `shape_ground` | The Area tool's ground actions over a rectangle: smooth, naturalize, erode, restore, raise, lower, paint |
| `stamp`, `import_heightmap` | Press a stamp (Mountain, Mesa, Crater rim, Dunes, Rocky ground, your own) into the ground; put a heightmap picture into a rectangle |
| `dig_path` | The Path tool along a line: a river or moat, a cave roofed with boulders, a ramp, a flattened, raised, lowered or smoothed way |
| (masks) | `shape_ground`, `dig_path`, `stamp` and `import_heightmap` take a mask, as the Mask panel: only some heights, slopes, biomes or paint |
| `reset_zones` | Mark zones for reset when you save (the game generates them anew), or unmark them |
| `regrow_nature` | Regrow the trees, rocks, bushes and pickables the game grows there, where nothing stands |
| `build_floor`, `build_walls`, `build_roof` | A floor, walls with doors and a gable roof (26° or 45°, thatch or shingle, gable ends closed) over a rectangle, from the game's pieces |
| `set_contents` | Fill a chest, put an item on an item stand (food on a flat one, a trophy or weapon on a wall) or dress an armour stand, write a sign, give a creature stars |
| `place_pieces`, `remove_objects` | Put down building pieces and other objects (they snap like the game's hammer); take objects away |
| `move_objects`, `set_transform` | Move and turn objects as one group (a building keeps its shape, optionally following the ground), or put each exactly: place, turn, tilt, size. They keep what they hold |
| `replace_objects` | Swap objects for another kind where they stand (a birch for a beech, stone walls for wooden ones) |
| `get_object`, `set_object_data` | Everything an object is and holds, as the inspector shows it; change its values: a portal's tag (to link two portals), a builder, a ward's permitted players, health, fuel, a creature's level or tamed state |
| `support_check` | The Workshop's support check: which pieces would fall |
| `open_workshop`, `add_blueprint` | The Workshop, empty or with a blueprint; add a blueprint of your library to the plot or to a world's area, turned as asked |
| `copy_objects` | Copies of objects, once or in a row or a ring (a row of houses, a fence, pillars round a hall) |
| `generate_dungeon` | A dungeon on the plot, or above a point of the world with its portals |
| `list_dungeons`, `dungeon_rooms` | The game's dungeons in the area (Frost Caves, crypts, mines…): their rooms, open ends, door joints and the rooms they can take |
| `add_dungeon_room`, `remove_dungeon_room`, `close_dungeon_ends`, `set_dungeon_door` | Change a game dungeon as the Dungeon tool does: a room at an open end (with what the game puts in it), a room taken out, end caps on every open end, a door at a joint |
| `undo`, `redo` | As Ctrl+Z and Ctrl+Y |

Each of these changes is one step of the history, and it stays pending: nothing reaches the world's
files or the game until you **Save to world** or **Apply live**, and the Workshop's building becomes
a blueprint when you **Save blueprint**. (A dungeon generated on the plot is also kept in your
library as its own blueprint, as the Workshop's **Generate dungeon** does.)

While the open world is **live with Auto on**, Claude changes nothing (each change would reach the
game at once): turn Auto off, and Claude's changes wait for **Apply live**.

## Asking

Open the world (or the Workshop) yourself, or ask Claude to. Then, for example:

- "Open the area around 1200, -340 and make a valley with a river running north, with birches along
  it."
- "Flatten a 40 m square in the middle of the area, pave it, and put a ring of torches round it."
- "In the Workshop, build a small longhouse: stone floor, wooden walls with two doors, a 45° thatch
  roof. Check that it stands." (Claude Code and Claude Desktop also offer the editor's prompts:
  *build_in_workshop* and *shape_area*.)
- "Open the area around 800, 950 and generate a two-level Swamp crypt above it."

Claude checks its work with screenshots and, for buildings, the support check. Ground work, forests
and dungeons are the easiest to ask for; detailed buildings, placed piece by piece, may take a few
rounds ("the roof overhangs on the east side, fix it"), or start from one of your blueprints.

When you like the result: **Save to world**, **Apply live** or **Save blueprint**. If not: **Discard**,
or Ctrl+Z.

## The building skill (Claude Code)

For buildings that should look hand-made rather than placed by numbers, the repository has a Claude Code
skill, [`.claude/skills/valheim-build`](../.claude/skills/valheim-build/SKILL.md). With it, Claude first
looks up real Valheim builds and guides for ideas and writes a short design brief, shapes the ground,
tries unfamiliar pieces in a small mock-up before using them, builds in phases with a library of proven
helpers (glazed arched windows, storey ledges and cornices, battlements, round towers with spires, tile
roofs with stepped gables, bartizans, porches, timber-framed halls), then checks its own work: scripts
that find blocked windows, doubled pieces and stones sitting on windows, and a close look at every
junction. Interiors get framing, a focal point, lighting and furnishing; the outside gets paths, lights
and planting.

Claude Code loads it by itself when you work in this repository. To have it everywhere, copy the
folder to your own skills: `cp -r .claude/skills/valheim-build ~/.claude/skills/`. Then ask as usual,
for example "Build a Gothic castle with a moat in the Workshop" or "this hall looks bare, furnish it".
What it learns about pieces (sizes, which way they face) is in its `references/pieces.md`; corrections
are welcome there.
