---
name: valheim-build
description: Design and build in Valheim through the Valheim World Editor's MCP tools (valheim-editor) — castles, houses, villages, towers, bridges, interiors, terrain shaping and landscaping — researching real Valheim builds for inspiration, mocking up details, building in phases, auditing and inspecting until it looks hand-made. Use this skill whenever the user asks to build, design, decorate, furnish, landscape or reshape anything in a Valheim world or the editor's Workshop, or says a build looks bare, rough, copy-pasted or unrealistic, even if they don't mention the editor or MCP by name.
---

# Building in Valheim with the world editor

Valheim builds are judged by the eye, piece by piece. The editor's tools make it easy to place thousands of
pieces quickly, and that is the trap: a fast parametric build reads as "copy-paste". This skill is the
workflow that turned a bare stone box into a castle the user liked. Every step exists because skipping it
produced something the user rejected.

The tools are the `valheim-editor` MCP server (`mcp__valheim-editor__*`; load them with ToolSearch if they
are deferred). Read the server's instructions when it connects: everything stays pending, the user saves.

## The loop

1. **Pre-flight**: `editor_state`. Open what is needed (`open_workshop` for a blueprint, `open_world` +
   `open_area` for a place in a world). Opening is refused over unsaved changes: ask the user, never discard.
   While the world is live with Auto on, nothing can change: ask the user to turn Auto off.
2. **Brief and research** (below). Write a short design brief before placing anything.
3. **Site**: shape ground first (terraces, moat, paths, plinth for the building).
4. **Mock-up**: build one bay of the new detail vocabulary off to the side, look, fix, then delete it.
5. **Build in phases** with `run_script` and the helper library, looking after each phase.
6. **Audit by script**, then **inspect visually**, junction by junction. Fix, re-audit.
7. **Interiors** and **dressing** (inside and out).
8. **Report**: what was built, what was fixed, what is left, what can't be judged from screenshots.

Don't stop after the first pass that "works": the user will find every incoherence (a window behind a
roof, a block on a window's corner, a sill turned wrong on a round tower, a door facing inward). Finding
them yourself first is the job.

## 1. Brief and research

Search the web before designing anything bigger than a hut (WebSearch, then WebFetch the useful pages;
fandom.com often refuses fetching, try other mirrors). Look for: real Valheim builds of the same kind,
build guides, and the real-world architecture it imitates (a photo of a real castle, Hluboká, was once the brief).
Read `references/techniques.md` for what research already established. If the user gives an image,
describe its parts first (massing, roofline, window rhythm, materials, signature features).

The brief, a few lines:
- **Story and roles**: what each part is for, because roles decide materials. Example from the castle:
  living wings = red tile roofs, crow gables, chimneys, a window grid; defensive towers = battlements and
  conical spires behind them; gate = portcullis, oriel, bartizans.
- **Palette**: 2–3 materials with a reason each (pale stone walls, red grausten tiles, dark shingle
  spires, darkwood trim, black marble accents).
- **Massing and plan**: footprints in metres, heights, a grid (2 m pieces; windows on a 4 m grid shared by
  every wing so rows line up across the building).
- **Signature features** (3–5) and the **site** (moat, terraces, approach road, gardens).

Asymmetry and height variation between buildings; regularity within one facade.

## 2. Coordinates, camera, workshop facts

- World metres: x east, z north, y up. Workshop plot: 192 m square, ground at y = 34, water at y = 30
  (digging below 30 fills with water, so a moat is just `Ground.Set` below 30).
- `screenshot` yaw: 0 looks north, **90 looks west**, 180 south, 270 east. The camera sits `distance`
  metres back from the point; it clips into walls and roofs easily, so for rooms keep the distance smaller
  than the room and the point inside it, and for exteriors go wider or higher. `views: 4` gives an overview.
- Faces turned away from the editor's sun look dark: judge materials on the sunlit side.
- `build_walls`/`build_roof` return huge JSON for big jobs: prefer `run_script` for anything large.

## 3. Mock-ups: learn a piece before using it

`piece_info` bounds are placeholders (1×1×1) for many marble, grausten and furniture pieces, and nobody
remembers which way a model faces. Before a new piece goes into a large build:
1. Place a swatch row or a one-bay mock-up off to the side (far corner of the plot), each variant at a
   different yaw if orientation is unknown.
2. Screenshot it close up, from the side that matters.
3. Write down what you learned (`references/pieces.md` has everything verified so far — read it first,
   it saves most mock-ups).
4. Delete the mock-up (remove by area in a script) before building for real.

## 4. Building with scripts

`scripts/lib.cs` is a C# helper library for `run_script`: windows (leaded glass, tall, barred, carved),
facades with plinth and storey ledges, cornices, battlements, piers with spires, flat roofs, red tile
roofs, crow gables, chimneys, cone spires, round towers, bartizans, porches. `run_script` scripts are
self-contained: Read the library, paste the helpers you need at the top of the script, then the build.
Read `script_reference` once per session for the script API.

- One phase per script (shell, roofs, towers, details…), each one undo step labelled "Claude: …".
- Use `dryRun: true` on anything large or destructive first.
- After each phase: screenshot (overview + one close-up). Fix before moving on.
- Pieces carry yaw only (`Objects.Place(prefab, x, z, yaw, scale, y)`); keep scale 0 for building pieces
  (the game doesn't keep scale on pieces).
- Every part of a window (arch head, panes, lattice, sill) takes yaw = the window's facing angle φ. Never
  snap a sill or pane to 0/90: it breaks on round towers.
- Stone may overhang freely in the editor, but if the user cares about the game's support, run
  `support_check` (Workshop) and add posts; ask once whether the user wants the build to stand in game.

## 5. Audit, then inspect

Run the audit scripts in `references/audit.md` (dry runs) after every big phase and before reporting:
duplicate pieces where blocks share a wall line, windows blocked by another block or a roof (tested at the
middle and both edges, low and high), small stones on a window's opening or arch, unpaired arch pieces,
orphan glass, floating flagpoles. Fix with scripts, re-run until clean.

Then tour visually: overview from four sides, then every junction — where wings meet towers, roofs meet
walls, a turret beside a taller tower (it must rise above it; colliding cones look broken), gates, bridges,
corners. Look for: things floating, things sunk, open roof ends (add crow gables), faces showing a tower's
outside inside a room (hide with a straight wall), rough-looking pieces (stone_fence reads as a cobble
pile — use blackmarble_2x1x1 or stone_wall_2x1 rails).

## 6. Interiors

Rooms need floors at storey heights, doors from the courtyard, stairs (wood_stair rises 1 m per 2 m;
leave the stairwell open in the floor above), and then the things that make them feel lived in, in this
order of impact (research and the user's reaction agree):
1. **Exposed timber framing**: darkwood_pole4 posts on the walls every 4 m between windows,
   darkwood_beam4x4 ceiling beams across, darkwood_beam_45 knee braces. This alone turns a stone box
   into a hall.
2. **A focal point** per room: a hearth under a chimney, the throne on a dais, the forge against a wall.
3. **Zones**: tables with benches, a work corner, a sleeping corner — furniture grouped, not scattered.
4. **Layered light**: hanging braziers, sconces on posts, candles on tables, standing braziers.
5. **Texture and clutter**: different furs (bear, wolf, deer), banners on posts, carvings
   (darkwood_wolf/raven), firewood stacks, barrels, chests, pots; food on tables with the serving tray's
   pieces (Feaster: every cooked dish, mead and feast is a placeable piece); items on item stands and armour
   on armour stands via `set_contents` (needs an editor with stand support).

Check every furniture piece's facing in a screenshot from inside the room (chairs toward tables, beds with
the headboard on the wall, ovens opening into the room, banners flat against the wall and 0.4 m off it).
Ground floors: stone_floor_2x2 centred at ground − 0.44 (not −0.5, or grass flickers through) and paint
the ground "paved" under rooms.

## 7. Exterior dressing and landscape

Porches (shingle canopy on two posts) over doors, lantern posts along paths (piece_dvergr_lantern_pole —
its origin is at the top), torches at bridge ends, braziers at the gate, hedges (Bush01) along paths but
never on them, trees framing views but never in front of the main facade, paved paint for roads and
courtyards, a moat with a stone bridge (pier, side arches, parapet). Clear trees from the build's footprint
and from the moat band first.

## 8. Reporting

Say what was built (by part, with sizes), what the audit and tour found and fixed, and what can't be judged
from the editor (the dark side's lighting, walking scale, how it looks in game). Offer next steps. Everything
is pending: remind the user to save (Save blueprint in the Workshop, Save for a world).

## Learning

When the user corrects something or a mock-up teaches a new piece fact, add it to `references/pieces.md`
(facts) or this file (process) so the next build starts from it.
