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
| `open_area` | Open the area around a point in 3D (1 to 5 zones of 64 m across) |
| `describe_area`, `list_objects` | Heights, biomes and objects of the area; objects by kind, name or place, with their ids |
| `screenshot` | A picture of the 3D view (from any camera, or four views of a place in one picture) or of the map, so Claude sees what it did |
| `area_map` | A map of the area from above, drawn from its data: heights, water, painted ground, objects, a grid in world metres |
| `get_selection`, `select_objects` | What you selected in the editor ("these trees"); Claude selecting objects to show you which it means |
| `building_guide` | How Valheim's pieces fit and hold (the 2 m grid, roofs, support), from the game's data |
| `find_prefabs`, `piece_info` | The game's objects and building pieces: names, sizes, snap points, cost |
| `list_blueprints` | Your blueprint library |

| Changing (pending) | |
|---|---|
| `run_script` | A C# script on the area, as the [Script tool](scripting.md) runs it (`script_reference` gives Claude the API): ground, paint, objects; a dry run says what it would change |
| `flatten`, `paint_area`, `road`, `forest` | Level a rectangle, paint ground, lay a road through points, plant trees or rocks |
| `build_floor`, `build_walls`, `build_roof` | A floor, walls with doors and a gable roof (26° or 45°, thatch or shingle, gable ends closed) over a rectangle, from the game's pieces |
| `set_contents` | Fill a chest, put an item on an item stand (food on a flat one, a trophy or weapon on a wall) or dress an armour stand, write a sign, give a creature stars |
| `place_pieces`, `remove_objects` | Put down building pieces and other objects (they snap like the game's hammer); take objects away |
| `support_check` | The Workshop's support check: which pieces would fall |
| `open_workshop`, `add_blueprint` | The Workshop, empty or with a blueprint; add a blueprint to the plot |
| `generate_dungeon` | A dungeon on the plot, or above a point of the world with its portals |
| `undo`, `redo` | As Ctrl+Z and Ctrl+Y |

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
