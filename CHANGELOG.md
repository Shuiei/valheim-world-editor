# Changelog

All notable changes to the Valheim world editor and its WorldEditorBridge plugin, which share one
version number. Newest first. Changes to the plugin are under **WorldEditorBridge** in each version;
the plugin is unchanged in versions without that part.

## v1.0.0 — unreleased (the native app replaces the web editor)

### Added
- Native app: the game's look (terrain textures, map textures and models) is copied from your own
  Valheim on first start and again after a game update, as the web editor did. The start page
  shows the copy's progress, asks for the Valheim folder when it is not found, and offers Try
  again when it failed. Choosing another game folder in Settings checks it again.
- Native app: a log (log.txt in the data folder, as the web app kept), the window's icon, the
  version in the title, and a Documentation link on the start page.
- Native app: a world folder (`ValheimWorldEditor <folder>`) or a live game (`--live <url> --token
  <token>`) given on the command line opens straight away, as with the web app.
- The developer checks of the world generator and the save writer (`--verify`, `--verify-ingame`,
  `--selftest-save`, `--inspect`, `--summary`) moved to a separate tool, `tools/WorldCheck`.

### Fixed
- Native app, Area: after choosing a backup folder with "Another folder…", the folder picker opened
  again and again; a folder chosen by hand was also dropped for live worlds.
- My game: on Linux, mod manager profiles were listed and searched twice.
- A character file that cannot be read no longer stops worlds from opening.

### Removed
- Native app: the development command-line options (pictures, scripted tools); `--world`,
  `--zone` and `--size` remain.

## v0.41.1 — 2026-10-07

### Fixed
- Apply live: an error answered by the game (the plugin refusing a call) is reported as "Could not
  apply live" instead of failing; the changes stay waiting.
- Apply live: when the game refused the objects, deletions were already counted as sent and were
  never sent again. Now nothing counts as applied until the game accepts it.
- Apply live twice at once (a double click) could create the same new objects twice in the game.
- Native preview, Select: Drop (End) right after selecting an object above another one put it too
  low, by as much as it floated above the ground.
- Native preview, Place: a double-click on the last point of a line or zone did not place it (the
  click was taken for dragging that point).
- Native preview, map: the 1 m close-up never showed when zoomed in (its place was passed to the
  shader as whole numbers, which OpenGL refused); the coarse whole-world map was drawn instead.
- Native preview: after going between the map and the 3D editor, both kept OpenGL objects from the
  view's previous context and drew with them (errors, or the wrong buffers); they are rebuilt now.
- Native preview, Place: a circle of walls end to end came out one wall short (a gap), the last
  one falling a hair past the end of the ring through rounding.
- An object whose data is edited or moved keeps the game's short position form (the terrain
  compiler grew 8 bytes when written back).

## v0.41.0 — 2026-10-07 (plugin settings file renamed)

### Changed
- The editor finds the plugin's settings under either name (your game, and the server's Valheim
  folder over SSH), so it works with older plugins too. Every hint and guide names the new file.

### WorldEditorBridge
- The plugin's id is now `Tie.WorldEditorBridge`, so its settings are in
  `BepInEx/config/Tie.WorldEditorBridge.cfg` (was `local.worldeditorbridge.cfg`). On its first start
  the plugin moves the old file to the new name: the port and token are kept, and saved servers in
  the editor keep connecting.

## v0.40.1 — 2026-10-07

### Changed
- The frame rate log is written only on demand: **Record frame rates** in Help, off each time the
  editor opens.

## v0.40.0 — 2026-10-07 (3D resolution)

### Added
- **3D resolution** (View, Look): Sharp (the screen's own pixels), Balanced (one per screen point:
  a quarter of the pixels on a screen scaled ×2) or Fast. Fewer pixels give more frames per
  second; the frame rate logs say which one was used.

## v0.39.0 — 2026-10-07 (frame rate log)

### Added
- **Frame rate log**: a line every 0.2 s while the 3D view is used (camera moving, or still while
  something else happens), with frames per second, the editor's work per frame and the longest gap
  between frames, in `perf-window.log` (the app's window) or `perf-browser.log` (`--browser`) in the
  data folder, to compare the two. Header lines give the browser, the view's size and what is loaded.

## v0.38.3 — 2026-10-07

### Fixed
- The frame rate line in Help could not be selected to copy it: it was rewritten several times a
  second, which dropped the selection. It now stays as it is while selected.

## v0.38.2 — 2026-10-07 (frame rate in Help)

### Added
- **Help** (`?`) shows the frame rate while the view moves, and how many milliseconds of work each
  frame takes, to tell a slow graphics path from a busy editor.

### Fixed
- Help said "the graphics card" for the app's own window on Linux and Windows: WebKit answers "Apple
  GPU" whatever draws the view, so the window now says it does not tell.

## v0.38.1 — 2026-10-07 (fixes found by new tests)

### Fixed
- Area tool: after Undo or Redo, the counts of what is inside the selection and the Replace list
  still showed what was there before (the Replace list could be empty while trees were back).

## v0.38.0 — 2026-10-07 (lighter 3D view)

### Changed
- The 3D view is drawn only while something happens (the view moves, a stroke, the mouse or keys
  in use) and a few times a second otherwise (enough for the game look's water and sky): an idle
  editor leaves the processor alone instead of drawing 60 times a second.

### Added
- **Help** (`?`) says what draws the 3D view: the graphics card, or the processor (llvmpipe,
  SwiftShader), which is much slower; the editor says so once when it is the processor.

## v0.37.0 — 2026-10-07 (pieces that snap, elevation)

### Added
- **Snap to pieces already there** (Place tool, building pieces): a wall, fence or floor locks onto
  the pieces around it like the hammer, **Beside** (end to end, at their level) or **On top** of
  the piece under the cursor, facing like it (Rotation turns it in quarter turns). Pieces are placed
  one at a time under the cursor.
- **Elevation** for everything placed: on the ground, a height **above the ground**, or **at one
  height** for all; `PgUp` / `PgDn` change it, `Alt` + click takes the top of a piece or the ground.
- **Layers** (Line, End to end): a line of walls or fences stacked several pieces high.
- A line started next to a piece already there continues it, at its level.

### Fixed
- Walls and other pieces with their origin in the middle were half sunk into the ground when placed
  one by one: they now stand on their bottom.

## v0.36.0 — 2026-10-07 (tidier tool panels)

### Changed
- **Tool panels** show the settings you use most, and fold the rest under one line at the bottom
  that says what is inside (shape, falloff, stamp, curve, clumping, tilt, facing...), open or closed
  as you left it. The **Mask** moves into that fold, which says "mask on" while it is on.
- **?** next to the tool name shows what the tool does and how to use it; the how-to text is hidden
  otherwise (in every tool, until you click ? again).
- **Area**: one **Action** list (Ground, Objects inside, Copy, Bring back) instead of six sections.
  The panel shows only that action's settings, and one button at the bottom does it (`Enter` too).
- **Place**: the panel lists the chosen kinds with their weights and a ✕ each; **+ Add kinds** opens
  the list of every kind (search, favourites, recent, Pick from world) in a drawer beside the panel.
- **Select**: Delete, Whole building, Same kind, Invert and Exact place first; Replace with, saved
  selections, Inspect data and Make player built under **More actions**; the moving switches under
  **Moving**.
- **Path**: Soft edge, curve and natural look are folded; Apply and Clear stay at the bottom.

## v0.35.0 — 2026-10-07 (script console)

### Added
- **Script console** (Script in the top bar, like MCEdit's filters): a few lines of JavaScript with
  a small API (find, add and remove objects, read and set the ground, work inside the Area
  selection) for jobs the tools do not cover. Each run is one undo step. Comes with examples (count
  kinds, remove young beeches, rocks on steep slopes, terraces); your own scripts are kept in this
  browser. See docs/script.md.

## v0.34.0 — 2026-10-07 (cut and fill)

### Added
- **Cut and fill** in the Area tool: how much ground inside the selection has been raised and dug
  since it was generated, and what Flatten, Raise or Lower would dig and fill (m³) before you click,
  with what the ±8 m limit leaves out.

## v0.33.0 — 2026-10-07 (overlays)

### Added
- **Ward areas**, **Build ranges** and **Location flattening** in View, Overlays: rings on the
  ground where wards protect, where crafting stations let you build (by kind: 10 to 40 m), and where
  locations flatten the ground.
- **Go to** a player (View, live mode): right there, or the area moves to them.

### Fixed
- After Undo or Redo in the Place tool, its spacing still counted the objects as they were before.

## v0.32.0 — 2026-10-07 (walk and fly)

### Added
- **Walk** and **Fly** views (`F`): see the world from a player's eyes, 1.8 m above the ground (or the
  water), and walk it with WASD (Shift runs); `F` again flies (Space up, C down); `F` again goes back
  to the usual view. Tools keep working, and Follow and the Area arrows keep the view.

## v0.31.0 — 2026-10-07 (area that follows the view)

### Added
- **Follow** (next to the Area arrows): when the point you look at comes within 12 m of the edge of
  the area, the area moves there by itself. It waits, and says why, while a stroke, a path or Place
  shape, an Area selection or selected objects would be lost.

### Changed
- Moving the area (arrows or Follow) keeps the view on the same spot and the tool in hand, instead
  of starting over above the middle with the Raise tool.

## v0.30.0 — 2026-10-07 (history survives reloads)

### Changed
- **History** survives a reload of the page and a move of the work area: the editor keeps it with the
  pending changes. A change that touched ground outside the new area is left out, with the ones
  before it. Saving, Discard and reloading from the game start a new history, as before.

### Fixed
- A long message in the status bar wrapped onto several lines and covered the last buttons of the
  tool panel, which then could not be clicked. It now keeps to two lines (the whole text on hover).
- "Switched on … in View" replaced what the tool had just said (how many objects were placed or
  regrown); it is now added to it.
- Regrow nature offered kinds the page cannot place, and then reported them as regrown.

## v0.29.0 — 2026-10-07 (regrow nature)

### Added
- **Regrow nature** (Area tool): puts back the game's own vegetation inside the selection, for the
  kinds chosen with the chips: the trees, rocks, bushes and pickables its rules grow there (how
  many, on which slopes, heights and biome edges, in groves), worked out like the game does
  (its rules and random draws, per zone), on the ground as it is now. Spots where something
  already stands are skipped, and so is the ground near buildings. On a world the game generated,
  most of what it would place lands exactly where the game put it.

## v0.28.0 — 2026-10-07 (clumping)

### Added
- **Clumping** and **Patch size** in the Place tool (Brush, Zone): objects gather in groves with
  clearings between them, following a noise pattern fixed to the world, instead of an even spread.
  Presets keep them (Meadows woods, Black forest, Berry patch and Meadows rocks use some).

## v0.27.0 — 2026-10-07 (weighted mixes and presets)

### Added
- **Weights** in the Place tool: with several kinds ticked, a slider per kind sets how often it is
  used (Beech 3, Birch 1: three beeches for one birch), with its share in %.
- **Presets**: load a mix (kinds, weights, Density, Spacing, Size, Tilt, facing) from built-in looks
  (Meadows woods, Black forest, Swamp, Berry patch, Forest floor, Meadows rocks) or save your own.

## v0.26.3 — 2026-10-07 (spacing that fits)

### Added
- **Place tool**: when a ticked kind is wider than the spacing, the panel says so and **fit** sets
  the spacing to it. A blueberry bush is about 3 m wide: a 1 m grid of them was one big hedge
  instead of rows.

## v0.26.2 — 2026-10-07 (editable zone points)

### Added
- **Place tool, Zone**: drag a point to move it, drag the outline to add a point, Ctrl + click a
  point to remove it (as with lines). The hints above Place say how, for lines too.
- **Remove last point** button (Zone and Line), the same as Backspace.

### Changed
- **Leave saplings and crops room to grow** also shows for grown crops (Pickable_Carrot...) and for
  trees that grow from saplings (Beech1, Oak1...): they keep the room their sapling needs (read
  from the game's saplings).
- **Zone** fills at random only: its Scatter / Grid choice is gone, Grid mode makes grids.
- **Grid** gets as many whole cells as fit best in the box you drag (at least one), centred: a box
  a little short of N cells used to lose a row, a small box got nothing. Its **Cell** slider is now
  called **Spacing** (space between objects).

## v0.26.1 — 2026-10-07 (placed objects follow View, chip toggles, Nobody builds)

### Changed
- Objects you placed follow the View switches like the others (before, they stayed shown until
  saved): switching off Trees & logs hides the trees you just planted too.
- A click on a **Favourite** or **Recent** kind ticks or unticks it (before, it placed only that
  kind, which Shift + click now does), and each row has **all** / **none**.

### Added
- **Untick all** in the Place tool, next to Pick.
- **Nobody (not player built)** in Built by: places pieces without a builder.

## v0.26.0 — 2026-10-07 (Place tool)

### Changed
- The **Plant** tool is now called **Place** (still `T`): it places any kind of object, walls and
  fences end to end along lines, circles and rectangles as well as trees and rocks with a brush.
  Its guide is now docs/place.md.
- **Leave saplings and crops room to grow** only shows (and only applies) when a sapling or crop is
  ticked.

## v0.25.0 — 2026-10-07 (player-built pieces)

### Added
- **Built by** (View panel, Building): the player written as the builder of the pieces you place.
  It lists the world's builders (the one with the most pieces is chosen at the start), the players
  named on beds, wards and tombstones, and the characters on this computer; any other player id can
  be typed. Remembered per world.
- **Make player built** (Select tool): gives selected pieces that have no builder the chosen one.

### Fixed
- Pieces placed by the editor were not always **player built**: kinds the world had none of, and
  copies of pieces from ruins, had no builder, so the game took them for ruins (a third of the
  materials back, no base for fires, ignored by raids, wards and private chests owned by nobody).
  Now every piece of a kind the hammer, hoe, cultivator or serving tray can place gets the chosen
  builder when it is placed, pasted or built. Moved and edited objects keep their builder.

## v0.24.0 — 2026-10-07 (eyedropper and favourite kinds)

### Added
- **Eyedropper**: the Plant tool's **Pick** button and the **pick** buttons next to the Replace lists
  (Select tool, Area tool) take the kind of the object you click in the world.
- **Favourites** and **Recent** kinds in the Plant tool: star kinds with ☆ in the list, and the last
  eight kinds you placed come back as buttons above the list. A click plants only that kind, Shift +
  click adds it to the ticked ones.

### Changed
- The Select tool's Replace row reads "Replace with", and its button is now "Replace the selection".

## v0.23.0 — 2026-10-07 (sharp edges)

### Added
- **Sharp edge** brush falloff: full strength right to the brush's edge and nothing beyond, for
  steep walls like the game's pickaxe.

### Changed
- With **Soft edge 0**, Path and Area actions give the ground points their edge crosses a share of
  the change, so a diagonal edge follows the line straight instead of stepping from point to point.

## v0.22.0 — 2026-10-07 (fine-tune drawn lines)

### Added
- **Fine-tune drawn lines** (Path tool, and Plant lines): drag a point to move it, drag the line to
  add a point there, Ctrl + click a point to remove it; the preview follows.

## v0.21.2 — 2026-10-07 (buildings keep their shape)

### Changed
- **Moving a building "on the ground"** keeps its shape: its pieces move as one block, and the block
  goes up or down until its bottom layer sits on the ground (sinking into a slope rather than
  floating). Trees, rocks and other objects in the selection still land on the ground one by one.
  Before, every piece was put on the ground by itself, pulling buildings apart.

## v0.21.1 — 2026-10-07 (ring brush outline)

### Changed
- The Ring brush shape draws its inner edge too, so the untouched middle shows on the ground.

## v0.21.0 — 2026-10-07 (exact moves and snapping)

### Added
- **Snap to other pieces** (Select tool, on by default): while moving, a piece's snap point locks
  onto the nearest snap point of a piece around it, so walls, floors and fences join like with the
  hammer.
- **Turning ring** around the selection: drag it to turn, Ctrl for 15° steps.
- **Exact place**: type the selection's position and turn, or how far to move and turn it.

### Fixed
- "Put each object on the ground" put a piece's middle on the ground for pieces whose origin is in
  the middle (walls), sinking them; their bottom rests on the ground now.

## v0.20.5 — 2026-10-07 (pieces follow the line)

### Changed
- **End to end switches itself on** in the Plant tool's Line mode when every ticked kind is a piece
  the game snaps (fences, walls, stakes...), and off for other kinds, until it is changed by hand.
- **Objects follow the line**: circles and rectangles always turn their objects along the outline,
  and "Follow the line" (before: Face along the line) is on by default for drawn lines. A piece now
  lies along the line (its length, not its front, follows it).

## v0.20.4 — 2026-10-06 (dropdown boxes)

### Fixed
- The closed dropdown boxes were drawn by the system theme (a light box in the app window on Linux)
  under the editor's light text, so the chosen value could not be read: the editor draws them
  itself now, dark, with its own arrow.
- A dropdown followed by a button (Replace … go, Paint … apply, Saved … keep) pushed the button onto
  the next line.

## v0.20.3 — 2026-10-06 (readable dropdowns)

### Fixed
- Dropdown lists could be unreadable (pale text on a pale background), depending on the system
  theme: the pages now declare themselves dark, so the lists are drawn dark, with the chosen entry
  highlighted in the editor's amber.

## v0.20.2 — 2026-10-06 (dropdown labels)

### Fixed
- In the app window on Linux, the group headings of the Replace lists ("Trees & logs"...) could show
  blank, with GTK warnings in the terminal: they read "Trees and logs" and so on now.

## v0.20.1 — 2026-10-06 (fences on slopes)

### Changed
- **End to end on slopes**: every piece stays level and stands on the ground where it is, a little
  higher or lower than the last one and touching it, like a fence going up a hill (before, each one
  sat on the lower of its two ends, which could leave it floating at the middle of a bump).

### Fixed
- End to end placed nothing at all when one of the ticked kinds had no known length yet; such kinds
  are now left out and the others are placed.

## v0.20.0 — 2026-10-06 (fences end to end)

### Added
- **End to end** (Plant tool, Line mode): pieces placed so that each one starts where the last one
  ends, turned along the line, like the game's hammer snaps them, from the pieces' own snap points
  (now in the build-piece catalogue) or the model's length for other kinds.
- **Circle and Rectangle lines** (Plant tool): press at the centre or a corner and drag to the size;
  **Close the loop** for drawn lines. With End to end the sizes snap to whole pieces, so a fenced
  ring or yard closes exactly.

## v0.19.2 — 2026-10-06 (moved objects on the ground)

### Changed
- **Moved objects land on the ground** (Select tool): each moved or turned object stands on the
  ground where it ends up. A new switch, "Put each object on the ground when moving" (on by
  default), turns it off to keep each object's height above the ground, as before (for buildings).

## v0.19.1 — 2026-10-06 (area selection)

### Added
- **Clear** in the Area tool, next to Box and Polygon, to start a new selection (as `Esc` does).

### Fixed
- The Area tool's selection outline stayed on screen in every other tool; it is only drawn in the
  Area and Paste tools now (the selection is kept for when you come back).

## v0.19.0 — 2026-10-06 (selection helpers)

### Added
- **Same kind, Invert and saved selections** (Select tool): select every object of the selected
  kinds, the opposite of the selection, or a selection kept by name for the world.

## v0.18.0 — 2026-10-06 (select a whole building)

### Added
- **Select a whole building** (Select tool, like Axiom's magic select): double-click a piece, or
  press Whole building, to select every piece connected to it.

## v0.17.0 — 2026-10-06 (river and canal)

### Added
- **River / canal** (Path tool): digs a U-shaped bed below sea level with sloped banks, so the sea
  flows in.

## v0.16.0 — 2026-10-06 (shape tool)

### Added
- **Shape** (tool `G`, like WorldEdit's `//generate`): one click puts a mound, cone, mesa, crater,
  moat, bowl or ridged hill into the ground, or any shape written as a formula.

## v0.15.0 — 2026-10-06 (erosion)

### Added
- **Erode** (sculpt tool `O`, and an Area action): thermal erosion settles slopes to a rest angle,
  water erosion cuts gullies and fills hollows.

## v0.14.0 — 2026-10-06 (heightmaps)

### Added
- **Heightmaps** (Area tool, like WorldPainter): export the area's ground as a 16-bit grayscale PNG
  and import a picture back into the selection or the whole area, between chosen heights.

## v0.13.0 — 2026-10-06 (stamps)

### Added
- **Stamps**: pictures as brush shapes (built in: mountain, mesa, crater rim, dunes, rocky ground;
  or any picture loaded from a file), and "Stamp once" to put a whole stamp into the ground with one
  click, to a chosen height.

## v0.12.0 — 2026-10-06 (brush shapes)

### Added
- **Brush shapes and falloff** (sculpt and paint tools, like WorldPainter): circle, square, ring or
  ragged brushes, with a smooth, linear, dome, flat-top or peak falloff; square brushes turn.

## v0.11.0 — 2026-10-06 (restore from a backup)

### Added
- **Restore from a backup** (Area tool, like WorldEdit's `//restore`): the ground and objects of a
  selection as they were in one of the world's backups (the editor's or the game's), objects with
  all their data.

## v0.10.0 — 2026-10-06 (reset zones across the world)

### Added
- **Reset zones across the world** (map page, like MCA Selector): pick zones by biome, distance
  from buildings, ground edits and distance from the centre, and mark them all for the game to
  generate again.
- **Zone resets in live mode** (needs WorldEditorBridge 0.10.0 or newer): Apply live resets the
  marked zones in the running game, which generates them again at once where players are.

### Fixed
- A zone reset also removed players' tombstones, with what they carried: they are always kept now.
- Saving twice within the same second failed, because both backups got the same folder name.

### WorldEditorBridge
- New: `POST /zones/reset`, so the editor can reset zones in the running game (they are generated
  again at once where players are). Players' tombstones are always kept.

## v0.9.0 — 2026-10-06 (search the world)

### Added
- **Search the world** (map page): every object of a kind, every container holding an item, or every
  sign, portal or ward whose text matches, counted and pinned on the map; a result opens the 3D
  editor with that object selected.

## v0.8.0 — 2026-10-06 (object inspector)

### Added
- **Object inspector** (Select tool, `I`): everything an object holds in the save, by name, and
  editing it: chest contents (items, stacks, quality, slots), sign texts, portal tags, ward
  permissions, timers... An edit is one undo step and is saved or applied live like any change.

### Fixed
- Objects placed or moved in this session lost what they were copied from after the page was
  reloaded, so moving one again made a fresh copy (a moved chest lost its contents).

## v0.7.0 — 2026-10-06 (repeat a paste)

### Added
- **Repeat a paste** (like WorldEdit's `//stack`): one click places several copies in a row, along
  the copy's width or depth or stacked upwards, with a gap; one undo step.

## v0.6.0 — 2026-10-06 (PlanBuild files)

### Added
- **PlanBuild and .vbuild files**: import a PlanBuild `.blueprint` (its terrain marks become ground)
  or a `.vbuild` file into the blueprint list, and write any blueprint back out in either format.

## v0.5.0 — 2026-10-06 (blueprints)

### Added
- **Blueprints** (Area tool → Copy & paste): save the clipboard as a named file, browse the saved
  ones with a picture of each, and paste them into any world. They are kept in the `blueprints`
  folder of the editor's data folder.

## v0.4.1 — 2026-10-05 (editor on GitHub only)

### Changed
- **Thunderstore carries only the plugin** (`Tie-WorldEditorBridge`): Thunderstore does not host
  programs and rejected the `ValheimWorldEditor_Windows` and `_Linux` packages. The editor is
  downloaded from the GitHub releases page, which the plugin's mod page now points to.

## v0.4.0 — 2026-10-05 (clean-up)

### Fixed
- Leaving the world map with **Worlds** while it was still loading could raise an error on the
  page (a cut-off map reply was read as data).

### Changed
- Unused code removed, and the origin notes of the files ported from the game corrected.
- Development: the old one-off test scripts are gone (the CI tests replace them), and the browser
  tests wait for the 3D view to be drawn instead of fixed pauses, so they no longer fail at random
  on a busy machine.

## v0.3.3 — 2026-10-05 (Thunderstore dependency)

### Changed
- **Thunderstore**: the editor packages no longer carry their own copy of the plugin: they depend
  on `Tie-WorldEditorBridge`, which mod managers install with them (one copy per profile; plugin
  fixes no longer need a new editor package). The GitHub downloads keep it in `plugin/`.
- The start page's plugin steps point to WorldEditorBridge on Thunderstore for mod manager users.

## v0.3.2 — 2026-10-05 (smaller packages)

### Changed
- **Only what the editor needs**: the bundled Python keeps just the files a full game-look export
  uses (traced on both systems; the export's output is byte-for-byte the same): 117 → 87 MB on
  Linux, 74 → 48 MB on Windows. Gone with it: `requirements.txt`, the Linux copy of `icon.ico`,
  and in the Thunderstore packages the `README.txt` files (the mod page replaces them).

### Fixed
- **Windows**: the bundled Python carries `msvcp140.dll`, which UnityPy needs; a Windows without
  the Visual C++ runtime could not copy the game's look.

## v0.3.1 — 2026-10-05 (Thunderstore packages)

### Added
- **The editor on Thunderstore**: `ValheimWorldEditor_Windows` and `ValheimWorldEditor_Linux`, the
  editor with the plugin, for r2modman and Thunderstore Mod Manager; `WorldEditorBridge` stays as
  the plugin alone, for servers.
- Installed by a mod manager, the editor finds its own profile for **My game**.

### Fixed
- Linux: the game-look export runs even when the bundled Python lost its run bit (mod managers
  unpack without permissions). The release no longer holds the `python`/`python3` links.

## v0.3.0 — 2026-10-05 (Thunderstore)

### Added
- **WorldEditorBridge on Thunderstore**: install the live-editing plugin with r2modman or
  Thunderstore Mod Manager; BepInEx comes with it.

### Changed
- **One version number** for the editor and the plugin (0.3.0, the plugin's version until now),
  kept in the `VERSION` file. The plugin DLL's file version said 0.1.0; it now says 0.3.0 too.

### WorldEditorBridge
First release on Thunderstore; from here on the plugin has the editor's version number: use the two
together.
- **Objects live**: deleted, planted, pasted and moved objects are applied in the running world,
  and undoing after applying takes them back in the game too.
- **Ground live**: terrain edits (sculpt, paint, roads, areas) are sent to the game and shown to
  every player at once.
- **World snapshot**: the editor opens the running world as the game has it now, without a save.
- Listens on `127.0.0.1:5182` only, with a random token in `BepInEx/config/local.worldeditorbridge.cfg`.


## v0.2.0 — 2026-10-05 (desktop app)

Download, unpack, double-click: no command line, no Python, nothing to install.

### Added
- **Its own window** on Windows and Linux (the system's web engine; the browser as fallback, or
  with `--browser`), with an icon.
- **Start page with three ways to edit**, each clearly separate:
  - **My game (live)**: finds Valheim running with the plugin (also in r2modman / Thunderstore Mod
    Manager profiles) and reads its token from your own game's settings; one click to edit live,
    or the steps that are missing (BepInEx, the plugin, starting the game).
  - **A dedicated server (live)**: the editor logs in over SSH (password or key, no `ssh` program
    needed) and makes its own tunnel to the plugin. The plugin token is always typed by you.
    Servers are saved in `servers.cfg` after the first connection for one-click access, with the
    password only when "Save password" is ticked; the server's SSH identity is remembered and a
    change is refused.
  - **A saved world (offline)**: the worlds found on the computer (Valheim's usual folders, Proton
    included), recently opened ones, and any other folder (with a folder dialog).
  **Worlds** on the map page goes back to the start page.
- **Settings** for folders that are not in the usual place: the Valheim game folder, extra BepInEx
  folders or mod manager profiles, and world folders that are always listed. For a server, an
  optional **Server's Valheim folder**: the plugin's port is read from it, and when the plugin does
  not answer the editor says exactly what is missing there.
- **Automatic game look**: the editor finds Valheim in the Steam libraries and copies the game's
  textures and models from it by itself, with progress on the page; it asks for the folder when
  Valheim is not found, and copies again after a Valheim update. The copy lives in the per-user
  data folder, next to the settings and a log.
- **One download per system**, each with the plugin in `plugin/` and instructions for that system.

### Changed
- The program is now called `ValheimWorldEditor`. It picks the first free port from 5180, so a
  second copy or another program never blocks it.
- The game-look exporter comes with its own small Python runtime; its texture fixes need only
  Pillow now (same visible result).

### Fixed
- Connecting to a server that does not answer gave up only after 3 minutes; the editor now checks
  the bridge first (at most 10 seconds) and says what is wrong: no tunnel, wrong token, or a game
  that does not host the world.
- The app quits cleanly when the system asks it to (logout, shutdown), closing its window.

## v0.1.0 — 2026-10-05 (first release)

Everything below, packaged: the editor for Linux and Windows (x64), and the WorldEditorBridge
server plugin (0.3.0) for live mode. Game files are not included.

### Added
- **Release packages** (`tools/release.sh`): program, web page and export tool, without source
  code or game files.
- **`export_all.py`**: one command that copies the game's terrain shader and textures, map textures
  and the models of every kind of object from your own Valheim install (a few minutes). Replaces
  the separate scripts that had paths of the development machine written in.

### Changed
- Models are now exported for every placeable kind (about 800 world objects plus 689 building
  pieces), not only the kinds one world happened to contain, so the Plant tool shows real models
  for far more kinds.

## 2026-10-05 — Placing, selecting and moving

### Added
- **Hover help on every control**: each slider, switch, button and list in the editor and on the map
  explains exactly what it does; tool buttons also show what their tool does.
- **Documentation**: a README with installation for offline and live mode, a page per feature in
  `docs/` with screenshots, and this changelog.
- **Select inside a zone.** In the Select tool, drag on empty ground (or Alt + drag anywhere, for
  forests) to draw a zone; everything shown inside it is selected. Shift adds to the selection.
- **Place any game object.** The editor now knows every network object in the game (about 1,500
  placeable kinds), so you can plant or place things the world has none of yet, such as turnips.
  Such objects are created exactly like the game creates a freshly placed one.
- **Leave saplings room to grow** (Plant tool, on by default). Saplings and crops are never placed
  closer than their in-game grow radius to anything; the panel also reminds you when a crop needs
  cultivated ground.
- **Drop to surface** (Select tool, `End`): drops the selection onto the object below it, or onto the
  ground when there is none.
- **Move arrows** (Select tool): red X, green Y, blue Z arrows at the selection; drag one to move along
  that axis only, Ctrl snaps to 0.5 m.
- **Plant: Line, Grid and Zone modes.** Place objects every N metres along a drawn line (optionally
  smoothed, facing along it, with sideways wiggle), one per cell of a dragged box, or fill a freely
  drawn zone by scatter or by an orderly grid. Zones and grids can be turned as a whole.
- **Foldable View panel.** Click a section title (Look, Nature, Spoilers, Overlays) to fold it; Look
  starts folded and your choice is remembered.

### Changed
- **Rotation in 1° steps everywhere** (`,` `.` and Alt+wheel), 15° with Shift: Select, Plant brush,
  Plant zones/grids and Paste. Paste can now be turned to any angle, not only quarter turns.
- **Flatten levels to the height where the stroke starts**, instead of a fixed 35 m that made it dig
  like Lower on higher ground. A fixed height is still available (untick the option, or Alt + click).
- **The Mask judges the ground as it was when the stroke started**, so raising past the mask's height
  limit or changing the slope no longer stops a stroke half-way. Settings that let nothing through
  (like a 0° maximum slope) are flagged, and an empty stroke says the Mask left everything out.
- Placing a kind whose View switch is off now switches it on, so what you placed stays visible after
  a reload.
- The move arrows are smaller and leave the object's middle free for a free drag.
- The editor tells the browser to revalidate its page files on every load, so an update is used
  right away instead of an older cached copy.

### Fixed
- With Game look off (or without the extracted game files) the world's objects were not loaded, so
  nothing could be planted, placed or saved as an object; they now always load.
- The world map no longer fails to load when the map textures are missing; it uses plain colours.
- Pressing a second key (End, PgUp, turn) right after a move acted on the hidden original instead of
  the moved copy.

## 2026-10-05 — History, discard and live objects

### Added
- **History panel** (`L`): every change with its time; "Back to here" rolls back to any point, and
  "Remove" takes out a single change while keeping everything done after it.
- **Move, turn, lift, copy and paste selected objects** in the Select tool (drag, `,` `.`, PgUp/PgDn,
  Ctrl+C / Ctrl+V).
- **Plant preview**: see-through "ghosts" show exactly what a click will place, for every kind; `R`
  rolls a new layout, Alt+wheel turns it; single placement mode; categories fold.
- **Objects live** (WorldEditorBridge 0.3.0): deleted, planted, pasted and replaced objects are
  applied to the running game; undo works after applying.
- **Ground live** (WorldEditorBridge 0.2.0): "Apply live" and "Auto" send ground edits to the game.
- **Live mode** (WorldEditorBridge plugin): the editor can open the running world of a server,
  with its players, instead of a save on disk.

### Changed
- Copies, plants and pastes create **fresh, independent objects**: a copied chest is an empty chest;
  moving an object keeps all of its data.
- **Discard** undoes only the changes that are not saved or applied yet, in place, without reloading
  the page (tool, view and zoom are kept).
- The pending counter only counts zones that really differ from their saved state, so undoing back
  to the start clears it.
- Objects you placed but have not saved yet are always drawn (with a green marker), whatever the
  View switches say.

### Fixed
- Raspberry bush leaves disappearing in the plant preview and with see-through buildings.
- The brush ring and other moving overlays disappearing when zoomed in close.
- A few kinds (Bush01, shrub_2 and others) drawn as boxes because the wrong model was matched.

## 2026-10-05 — World-editor tools

### Added
- **Masks** for brushes, paths, area actions and planting: by biome, height range, slope range and
  paint.
- **Area tool** (`B`): box or polygon selection; flatten, raise, lower, smooth, naturalize, restore or
  paint the ground inside; remove, select or replace objects inside; copy and paste ground and
  objects (turn and mirror); reset zones so the game generates them again.
- **Plant tool** (`T`): paint trees, rocks, bushes and more onto the ground with density and spacing;
  Shift + drag removes the chosen kinds.
- **Measure tool** (`M`): distance, height difference and slope between two points; slope colours
  and height lines in View.
- **Delete objects** (Select tool, `Del`), and a reworked interface: top bar, tool rail, tool panel
  and View panel.

## 2026-10-05 — In-game look

### Added
- The 3D editor draws the world like the game: the game's own terrain shader and textures, water
  and sky, real models for buildings, trees, rocks and bushes, and show/hide switches per kind
  (spoilers such as ore and ruins are off by default).

## 2026-10-04 — First versions

### Added
- **World map** in the browser, drawn with the game's own map shader, with player buildings, painted
  ground and edited zones.
- **Base terrain from the seed**: a port of Valheim's world generator that matches the game bit for bit.
- **3D terrain editor** with brushes (Raise, Lower, Flatten, Smooth, Naturalize, Restore), ground
  paint (Dirt, Cultivate, Paved, Clear), the Path tool, undo/redo, and the game's ±8 m limit.
- **Saving back to the world**: a new save number with a full backup and a read-back check.
- **CrossplayJoinCodeFix** (separate plugin, own repository): keeps a crossplay server from appearing
  offline when PlayFab answers the join-code check incompletely.
