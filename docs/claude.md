# Claude

![Settings with Allow Claude to connect on: the port, New token, the Claude Code command and the Claude Desktop configuration, each with Copy](images/settings-claude.jpg)

*Settings → Claude, switched on: copy the command into a terminal (Claude Code) or the lines into Claude Desktop's configuration.*

The editor can let **Claude** (in Claude Code, Claude Desktop, or any other MCP client) work on the world or the
Workshop you have open: look at it, shape the ground, plant forests, lay roads, build, generate dungeons. You
describe what you want in a prompt; Claude does it in the editor, step by step, and you watch it happen in the
3D view.

Whatever Claude changes stays **pending**, like your own changes: one step of the history each (`Ctrl+Z` takes
it back), counted in the save bar. Claude never saves a world and never applies anything to a live game: you do,
when you like what you see. It saves the Workshop's building as a blueprint only when asked
(`save_blueprint`), and never replaces one unless told to.

## How to use it

1. Turn it on in **Settings** (below), once, and add the editor to Claude.
2. Start the editor and open a world or the Workshop (or ask Claude to open one).
3. Ask Claude for what you want ([Asking](#asking)). Watch it work in the 3D view.
4. Keep what you like: **Save to world**, **Apply live** or **Save blueprint**. Take back what you do not like:
   `Ctrl+Z`, **Discard**, or **Take back Claude's changes** in History.

## Turning it on

In **Settings** (on the start page), section **Claude**:

| Control | What it does |
|---|---|
| **Allow Claude to connect** | On: the editor takes Claude's requests, on this computer only, from callers with the token. Off: nothing can connect. |
| **Claude may run scripts** | On: Claude may run scripts (`run_script`, the Script tool's Run). A script is a program with your rights on this computer, your files included, as any program you run; the process it runs in is not a sandbox. Off (the default): scripts from Claude are refused; its other tools still work. |
| **Port** | The port Claude connects to on `127.0.0.1` (5731 at first; 1024 to 65535). Change it if another program uses 5731. |
| **New token** | Makes another token. Once you **Save**, the old one stops working: add the editor to Claude again with the new command. |
| Claude Code command + **Copy** | The command that adds the editor to Claude Code. It holds the token: keep it to yourself. |
| Claude Desktop lines + **Copy** | The lines to add to Claude Desktop's configuration. |

Press **Save**. The editor now answers on `http://127.0.0.1:5731/mcp`, and the Claude section shows "Listening on
http://127.0.0.1:5731/mcp" (or why it could not, for example when the port is taken).

**Claude Code:** run the command once in a terminal:

```sh
claude mcp add --transport http valheim-editor http://127.0.0.1:5731/mcp --header "Authorization: Bearer <token>"
```

**Claude Desktop** starts a program rather than connecting to an address. Add the lines Settings shows to its
configuration (Claude Desktop → Settings → Developer → Edit Config, `claude_desktop_config.json`), then restart
it:

```json
{
  "mcpServers": {
    "valheim-editor": { "command": "/path/to/ValheimWorldEditor", "args": ["--mcp-stdio"] }
  }
}
```

`--mcp-stdio` opens no window: it passes Claude's messages to the editor that is running (which must have
**Allow Claude to connect** on), with the token from its settings.

**Other MCP clients** connect to the address (streamable HTTP) with the same `Authorization` header.

The editor must be running for Claude to reach it.

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
| `run_script` | A C# script on the area, as the [Script tool](scripting.md) runs it (`script_reference` gives Claude the API): ground, paint, objects; a dry run says what it would change. Needs your leave, once: **Settings → Claude → Claude may run scripts** (off by default) |
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
| `move_objects`, `set_transform` | Move and turn objects as one group (a building keeps its shape, optionally following the ground), or put each exactly: place, turn, tilt, size. They keep what they hold. A part of a group a mod links into one (a ship and the pieces on it) brings its whole group (also for copies and taking away) |
| `replace_objects` | Swap objects for another kind where they stand (a birch for a beech, stone walls for wooden ones) |
| `get_object`, `set_object_data` | Everything an object is and holds, as the inspector shows it; change its values: a portal's tag (to link two portals), a builder, a ward's permitted players, health, fuel, a creature's level or tamed state |
| `support_check` | The Workshop's support check: which pieces would fall, and the creatures sunk into a piece |
| `save_blueprint` | Save the Workshop's building as a blueprint of your library (with its picture); it never replaces one unless told to |
| `open_workshop`, `add_blueprint` | The Workshop, empty or with a blueprint; add a blueprint of your library to the plot or to a world's area, turned as asked |
| `copy_objects` | Copies of objects, once or in a row or a ring (a row of houses, a fence, pillars round a hall) |
| `generate_dungeon` | A dungeon on the plot, or above a point of the world behind the entrance of its kind (portals where the biome has none) |
| `list_dungeons`, `dungeon_rooms` | The game's dungeons in the area (Frost Caves, crypts, mines…): their rooms, open ends, door joints and the rooms they can take |
| `add_dungeon_room`, `remove_dungeon_room`, `close_dungeon_ends`, `set_dungeon_door` | Change a game dungeon as the Dungeon tool does: a room at an open end (with what the game puts in it), a room taken out, end caps on every open end, a door at a joint |
| `undo`, `redo` | As Ctrl+Z and Ctrl+Y |

Each of these changes is one step of the history, and it stays pending: nothing reaches the world's files or the
game until you **Save to world** or **Apply live**, and the Workshop's building becomes a blueprint when you
**Save blueprint**. (A dungeon Claude generates on the plot is not kept in your library: it is part of the plot's
building, pending like the rest. The Workshop's own **Generate dungeon** also keeps one as its own blueprint.)

## How it works

- **A server inside the editor.** With the switch on, the editor runs a small MCP server (the protocol Claude
  uses to call tools) on `127.0.0.1`, the address only programs on this computer can reach. When Claude
  connects, the editor gives it a short guide to working in it (how areas, zones, the ±8 m limit, pending changes
  and the tools fit together), and two ready prompts: *build_in_workshop* and *shape_area*.
- **Each tool does what you would do.** Claude's tools call the same code as the editor's buttons and tools: a
  `road` is the Path tool, `shape_ground` the Area tool, `run_script` the Script tool, `set_control` sets a
  control as your click would. So what Claude makes follows the same game rules as your own edits (the ±8 m
  ground limit unless No limit is asked for, snapping of building pieces, zones, locations).
- **Labelled in History.** Each step Claude makes is named "Claude: …" in History and carries a **Claude** tag.
- **Seeing its work.** `screenshot` and `area_map` give Claude pictures of the 3D view and of the area from
  above; `support_check` tells it which pieces would fall. It uses them to check and correct what it did.

## Safety

- **This computer only.** The server listens on `127.0.0.1` and refuses any request addressed to another name,
  so a web page you visit cannot reach it under a made-up address.
- **The token.** Every request must carry the token (48 random characters made for your editor). Programs and web
  pages that do not have it are refused. **New token** makes another one.
- **Pending only.** Claude never saves, applies live, discards, reloads, writes or deletes your files, or
  connects to a live game: those controls are refused to `set_control`, and no tool does them. `open_world` is
  refused while the open world or the Workshop has changes not saved.
- **Scripts need your leave.** A script is a program with your rights on this computer, your files included,
  like any program you run; the process it runs in is not a sandbox. `run_script` (dry runs too) and the Script
  tool's Run through `set_control` are refused until you turn on **Settings → Claude → Claude may run scripts**,
  a decision you take once, as for a document's macros. Claude's other tools (`flatten`, `road`, `forest`…) run
  the editor's own scripts and need no leave. A script you load into the Script tool from a file is your own
  choice, as before.
- **Live with Auto on: nothing.** While the open world is live with **Auto** on, Claude changes nothing (each
  change would reach the game at once) and tells you to turn Auto off; Claude's changes then wait for **Apply
  live**.
- **Off is off.** Turn **Allow Claude to connect** off and nothing can connect.

## Taking back Claude's changes

Claude's changes are ordinary steps of the history:

- `Ctrl+Z` takes back the last step, Claude's or yours.
- In **History**, Claude's steps carry a **Claude** tag. **Remove** on one of them takes that step out and keeps
  everything done after it.
- **Take back Claude's changes** (at the top of History, shown while Claude has changes in) removes every step
  Claude made in the history shown, newest first, and keeps yours, even those you made after Claude's. The status
  bar says how many were taken back.
- **Discard** throws away everything not saved, yours too.

Taking back works after saving or applying too: the step is removed in the editor, and the next **Save to world**
or **Apply live** writes that.

## Asking

Open the world (or the Workshop) yourself, or ask Claude to. Then, for example:

- "Open the area around 1200, -340 and make a valley with a river running north, with birches along it."
- "Flatten a 40 m square in the middle of the area, pave it, and put a ring of torches round it."
- "In the Workshop, build a small longhouse: stone floor, wooden walls with two doors, a 45° thatch roof. Check
  that it stands." (Claude Code and Claude Desktop also offer the editor's prompts: *build_in_workshop* and
  *shape_area*.)
- "Open the area around 800, 950 and generate a two-level Swamp crypt above it."
- "These trees I selected: replace them with beeches." (Select them first; Claude reads your selection.)

Claude checks its work with screenshots and, for buildings, the support check. Ground work, forests and dungeons
are the easiest to ask for; detailed buildings, placed piece by piece, may take a few rounds ("the roof overhangs
on the east side, fix it"), or start from one of your blueprints.

When you like the result: **Save to world**, **Apply live** or **Save blueprint**. If not: **Discard**, `Ctrl+Z`,
or **Take back Claude's changes**.

## The building skill (Claude Code)

For buildings that should look hand-made rather than placed by numbers, the repository has a Claude Code skill,
[`.claude/skills/valheim-build`](../.claude/skills/valheim-build/SKILL.md). With it, Claude first looks up real
Valheim builds and guides for ideas and writes a short design brief, shapes the ground, tries unfamiliar pieces in
a small mock-up before using them, builds in phases with a library of proven helpers (glazed arched windows,
storey ledges and cornices, battlements, round towers with spires, tile roofs with stepped gables, bartizans,
porches, timber-framed halls), then checks its own work: scripts that find blocked windows, doubled pieces and
stones sitting on windows, and a close look at every junction. Interiors get framing, a focal point, lighting and
furnishing; the outside gets paths, lights and planting.

Claude Code loads it by itself when you work in this repository. To have it everywhere, copy the folder to your
own skills: `cp -r .claude/skills/valheim-build ~/.claude/skills/`. Then ask as usual, for example "Build a Gothic
castle with a moat in the Workshop" or "this hall looks bare, furnish it". What it learns about pieces (sizes,
which way they face) is in its `references/pieces.md`; corrections are welcome there.
