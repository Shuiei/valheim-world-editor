# Changelog

All notable changes to the Valheim world editor and its WorldEditorBridge plugin, which share one
version number. Newest first. Changes to the plugin are under **WorldEditorBridge** in each version;
the plugin is unchanged in versions without that part.

## v1.114.0 — unreleased

### Added
- **Claude from another computer.** Settings → Claude has an **Address**: `127.0.0.1` (this computer only) as before,
  or `0.0.0.0` (every network) or one of this computer's networks, picked from a list that names each (or typed), so a
  Claude on another computer can connect, with the token. The command to add the editor to Claude Code follows the
  address; with every network, a line under it gives the address another computer uses (this computer's name).

## v1.113.0 — 2026-10-26

### Added
- **Show portals and their links** on the map: every portal of the world as a violet dot (hollow when it has no
  partner), a line between the two portals of each tag (the pair the game joins; a pair the save holds joined stays
  together), and the tags beside them when zoomed in. The switch's text counts the portals, the linked pairs and the
  portals alone. Off at first (spoiler). Portals a mod adds count too, by their name.

## v1.112.2 — 2026-10-26

### Fixed
- **Fewer antivirus warnings on Windows.** Every library that comes with the editor is now signed, and a web-server
  file the editor never used is no longer in the download.

## v1.112.1 — 2026-10-26

### Fixed
- **Reload and Discard keep the History in a live world.** Reading the world again from the game emptied History:
  now every step the game has stays there, applied and still undoable; only the steps not applied yet go, with their
  changes. Discard reads the world from the game when it has to, as it says it does.

## v1.112.0 — 2026-10-26

### Changed
- **History is kept whatever you save or apply.** Pasting a blueprint or a building, or shaping ground past the
  game's ±8 m, then saving or applying live, no longer empties History: undo it and save or apply again, and the
  ground comes back exactly as it was. Zone resets keep it too (only what the reset made again is left out), and it
  is all there when you open the world again.

## v1.111.1 — 2026-10-26

### Fixed
- **Leaning building pieces keep their lean when a world is opened.** A player-built piece that leans (pitched or
  rolled, as a blueprint built in the Workshop or a mod can place it) was read with only its turn about y: the 3D
  view, Claude and scripts showed it upright, and moving, turning or copying it (into a blueprint too) then saved it
  upright. It now keeps its pitch and roll as the world stores them.

## v1.111.0 — 2026-10-23

### Added
- **A Railroads add-on.** The track, stations and trains of the Railroads mod are drawn in the 3D view as the game
  builds them, from the mod's own models; track, trains and station signs you place, paste, move or turn with the
  editor work in the game as if built there, saved or live. It is its own download beside the editor's: unpack it
  into the addons folder.
- **Mods' extensions follow what you place, move and turn.** For mod authors: a placer (`AddPlacer`) is handed the
  objects of its prefabs each time the editor places, pastes, moves or turns them, by any tool, a script or Claude,
  to set what its mod works out from where they stand (a turn it keeps, the track under a vehicle); and a copy
  rewriter or a tool's edit can now set or take away a quaternion value (`ICopy.SetQuaternion`). See
  [Hooks](docs/modding.md#hooks).
- **Trains and other tied objects stay together.** A mod's `worldeditor.json` can tie objects to one that names them
  (a locomotive and its wagons, a deck and what is built on it): selecting, moving, turning, copying and deleting take
  the whole group, and each pasted copy gets ids of its own. See [Tied objects](docs/modding.md#tied-objects).

### Changed
- **Mods' objects an add-on draws show as drawn.** A mod's object the editor has no model of but an add-on or a mod's
  extension draws no longer shows the grey stand-in box inside its drawing, is picked and outlined by what is drawn,
  and shows in the Place and Paste previews.

## v1.110.0 — 2026-10-23

### Added
- **A ValheimRAFT add-on.** Ships and land vehicles built with ValheimRAFT are drawn whole, as the mod builds them,
  with their swivels, sails, ropes and rope ladders and what they carry, and move, copy and delete as one, live too.
  Its own download beside the editor's: see [ValheimRAFT](addons/ValheimRAFT/README.md).
- **For mod authors: more ways to place linked objects.** A link rule's `placements` give the parts hanging on one of
  its parent keys their own offset and turn keys, or let the group carry them where they were saved (a cart aboard a
  ship). An extension's link placer (`AddLinkPlacer`) turns the frame a parent's parts hang in (a swivel the mod has
  turned) or places a part itself, and shape hooks can read the ground's height (`IGroundView`). See
  [For mod authors](docs/modding.md#linked-objects).

### WorldEditorBridge
- **Mods know at once what the editor pastes.** Objects the bridge makes, moves or changes go through the game's own
  handling of objects that come from another game, so a mod that finds objects by ids it keeps (ValheimRAFT's ships
  and ropes) finds pasted ones right away: a host's game no longer drops the ropes of a ship pasted live.

## v1.109.0 — 2026-10-22

### Changed
- **Fly goes through the ground.** In Fly (`F` twice), `C` now takes the camera below the ground, into what a mod digs
  under it: Tunnelheim's tunnels and cellars, which the 3D view draws but no camera could reach. Space brings it back
  up. Walk still keeps the eyes 1.8 m over the ground, and the usual view turns around a point on it.

## v1.108.1 — 2026-10-22

### Fixed
- **Saving after an undo.** Undoing something you had saved (a placement, a deletion) and saving again now really
  takes it out of the world or puts it back: it said "Nothing to save" and left the world as last saved.
- **Saving and undo.** After a save, undoing a placement is counted as what the next save does (deleted, not added),
  offline and live; Discard puts back saved steps you had undone instead of losing them; the object count under the
  world's name follows your edits and undos; the map shows the new save number after saving.
- **Nothing lost in dungeons above.** The Mountain tool, scripts' `Ground.Mountain` and saving raised ground no longer
  remove trees and other things inside a dungeon 5000 m above them.
- **Copies land right.** Trees and rocks copied with the Area tool stand on the ground where you paste them; a moved
  tree in a wood stands on the ground, not on the other trees.
- **Keys stay where you type.** Typing an item name in a chest slot no longer sends the rest of the name to the editor's
  shortcuts.
- **Dungeons.** The Dungeon panel no longer says there is no dungeon right after you place one built of pieces; Go to
  the dungeon follows undo; the go-to buttons have the same names everywhere; the generator shows exactly the settings
  that apply to the game's own rooms, Loot included, and its summary names dungeons as the game does; building in a
  dungeon shows only the pieces, and its status line goes when you stop.
- **Workshop.** Save blueprint warns about falling pieces even with Support check off; a long cost line no longer
  pushes the top bar's buttons off the window.
- **Scripts.** Scripts move or remove a ship and its pieces as one, like the Select tool; the Terraced hill example only
  clears the nature it buries; an empty script says plainly that it has nothing to run.
- **Claude.** Opening a world closes the Workshop or the previous world properly; saving a blueprint again keeps its
  description and tags; flatten, road and paint stay within the game's ±8 m; unknown dungeon choices are refused
  with the valid ones listed; its errors go into the log.
- **The window.** The top bar fits narrower windows, live mode included; the right-hand panels no longer overlap; the
  locations note no longer covers the tools, and panels stop above a two-line status bar; cut-off labels in the Select,
  Path, Settings, map and new-world panels are shown whole; unsaved zones on the map stand out from saved ones.
- **Smaller things.** The new-world map can be dragged from anywhere; Measure's height difference follows the ground;
  the Select panel clears when undo removes what was selected; path and Place marks near the area's edge no longer
  spike; old messages ("Opening…", the Workshop's, a discarded world's) no longer linger; a Recent world that can't be
  opened says why; a link or the log your system can't open is named so you can open it yourself; a live world opened by
  its address no longer says "live" twice; a paste without its ground says its site was dug out instead of "with the ground"; zone resets now warn that tamed animals go too.
- **Texts.** About twenty tooltips and messages that said something untrue are corrected (the support colours, what
  Esc does, the ±8 m limit, presets, Follow, backups) and plurals and grammar fixed.

## v1.108.0 — 2026-10-22

### Added
- **Help for the tool you are using.** `F1`, on any page, opens the help at what you are doing: the tool in use, the
  Inspector, the map, a new world or the start page; each panel also has a small **?** at the top that opens its page.
  The documentation itself is rewritten: getting started, guides for common tasks, a page per tool, how things work,
  troubleshooting, every shortcut and a glossary, with new pictures.

## v1.107.0 — 2026-10-21

### Added
- **The documentation is now inside the editor.** **Documentation** on the start page opens it in its own window,
  beside the editor: every page with its pictures (click one to see it full size), a search through all of them,
  links between pages, Back and Forward. It works without internet and always matches your version of the editor;
  **Open in browser** shows the same page on GitHub.

## v1.106.0 — 2026-10-21

### Changed
- **Expand World is now an add-on.** Worlds made with the Expand World mods follow them with the Expand World add-on,
  its own download beside the editor's (unpack it into the addons folder); its switch under Add-ons in Settings
  replaces the Expand World switch. The Expand World folder setting is gone: add the BepInEx folder with the mods in
  Settings instead. Everything else works as before.

## v1.105.0 — 2026-10-21

### Added
- **Add-ons for mods that change the world through settings.** An add-on can now give the rules worlds are generated
  with, from a mod's settings and data files, on this computer or from a live game's own files, as Expand World's
  support does. See [World rules](docs/modding.md#world-rules).

## v1.104.0 — 2026-10-21

### Added
- **Clearer cards for worlds of world generation mods that cannot open.** A world made with such a mod stays in the
  world list, greyed out, and now says what is missing: the add-on for it turned off, the add-on waiting for its mod,
  the add-on not running, or the mod's own extension not turned on; and with the Riverheim add-on, a Riverheim too old
  for the world. See [World generators](docs/modding.md#world-generators).

## v1.103.3 — 2026-10-21

### Fixed
- **A mod's object changed twice before applying is updated in place in the game.** When a mod's tool changed the
  same object twice before the changes were applied live (two digs in one tunnel column, say), the game's object was
  removed and a new one made instead of being updated; with Tunnelheim, which keeps its records, the column then held
  two records.

## v1.103.2 — 2026-10-21

### Fixed
- **The New world page says when players need a mod.** With a world generation mod's generator chosen (Riverheim),
  the page still said the world was a normal one players need no mod for; it now says every player needs the mod,
  so console players on crossplay cannot join. The start page says so too, and the seed search no longer claims to use
  the game's own generator.

## v1.103.1 — 2026-10-21

### Fixed
- **Settings' add-on and extension switches take effect at once.** Turning an add-on or a mod's extension on or off
  in Settings changed nothing in the worlds listed until a world or the Workshop was opened: the list now follows the
  switches as soon as Settings is saved.

## v1.103.0 — 2026-10-21

### Changed
- **Riverheim is now an add-on.** Worlds made with Riverheim open with the Riverheim add-on, its own download beside
  the editor's (unpack it into the addons folder); its switch under Add-ons in Settings replaces the Riverheim switch.
  Everything else works as before.

## v1.102.0 — 2026-10-21

### Added
- **Add-ons for world generation mods.** An add-on can now teach the editor a world generation mod: reading and drawing
  the worlds it makes, and making new ones with it, as Riverheim's support does. See
  [World generators](docs/modding.md#world-generators).

## v1.101.0 — 2026-10-21

### Added
- **Add-ons can be turned off.** Settings, under Add-ons, gives each installed add-on a switch: off, the editor does
  not use it, and its folder stays where it is.

## v1.100.0 — 2026-10-21

### Added
- **Mods' extensions read the mod's own settings.** An extension can ask for settings from its mod's BepInEx settings
  files, as the world's game has them: a live server's own (the settings its players get), else the player's. See
  [Extensions](docs/modding.md#extensions).

## v1.99.2 — 2026-10-21

### Fixed
- **What mods' tools add is kept by the game.** Objects a mod's tool made (Tunnelheim's tunnel records, say) were saved
  as objects the game does not keep: the game took them away again once their area was unloaded, live at once. They
  are now saved as the game saves its own.

## v1.99.1 — 2026-10-21

### Fixed
- **Mods' extensions read the ground exactly as the game does.** Between the ground's points, the height an extension
  read could be a few micrometres off the game's; an add-on that keeps the ground in its data (Tunnelheim's records)
  now matches the game to the bit.

## v1.99.0 — 2026-10-21

### Added
- **A Tunnelheim add-on.** The release page now carries add-ons for mods beside the editor, each its own download to
  unpack into the addons folder; the first draws the tunnels Tunnelheim digs, as the game builds them.

## v1.98.2 — 2026-10-21

### WorldEditorBridge
- A folder of the game's BepInEx config folder that is replaced by a link while the editor asks for it is no longer
  listed: the folder is checked again once read, as a file already was.

## v1.98.1 — 2026-10-21

### WorldEditorBridge
- Settings files the editor reads from the game's BepInEx config folder are found through that folder's own list of
  files, so nothing the request names is ever used as a path, and every answer tells a browser not to guess its type.

## v1.98.0 — 2026-10-21

### Added
- **Mods' data goes with copies.** An extension can keep what its mod holds of a copied place, such as dug-out rock
  that no copied object carries, so it travels with the copy and its blueprint files and is put back where you paste,
  turned and mirrored with it, in the same undo step.

## v1.97.0 — 2026-10-21

### Added
- **Changes keep the game's object live.** In live mode, an object whose data you change (in the inspector, or with a
  mod's tool) now keeps its identity in the game instead of being removed and made again, so mods that track it, or
  that refuse to let their records be removed, keep working.

### WorldEditorBridge
- Objects can take new data in place, keeping their id, when the editor changes them.

## v1.96.0 — 2026-10-21

### Added
- **Add-ons.** Editor support for a mod can now come as an add-on of its own, made by anyone: unpack it into the new
  addons folder next to the editor, and it is used whenever the mod it is for is installed. Settings lists them.

## v1.95.0 — 2026-10-21

### Added
- **Mods' own tools.** An extension can add a tool to the rail, under Mods, with its own settings card: it is told
  where you point and click in the 3D view and which keys you press, previews its work, and its changes go in the
  history, undone and saved like the editor's own.

## v1.94.0 — 2026-10-21

### Added
- **Mods' extensions can draw whole areas.** An extension can now draw what its mod makes of an area (a canal, rock
  dug out of a hill) with the game's own ground look, in place of the editor's ground, and clicks land on it.

## v1.93.0 — 2026-10-21

### Added
- **More for mod authors' editor extensions.** A mod's file can now mark objects that only hold its data (the editor
  no longer shows them as boxes, and a zone reset that keeps buildings keeps them), and its extension can read the
  settings of the game's own scripts, as your copy of Valheim has them.

### WorldEditorBridge
- A live zone reset that keeps buildings also keeps the objects mods mark as holding their data.

## v1.92.0 — 2026-10-20

### Added
- **Live Riverheim worlds.** A game or server running a Riverheim world opens live, drawn with your own Riverheim when
  it is of the same release as the game's (else the editor says both versions).

## v1.91.0 — 2026-10-20

### Added
- **New Riverheim worlds.** With the Riverheim switch on and Riverheim installed, the New world page can make a
  Riverheim world: its map, marks and seed search show Riverheim's world, and the game opens it as a Riverheim world.

## v1.90.0 — 2026-10-20

### Added
- **Riverheim with Expand World.** A Riverheim world played with the Expand World mods is drawn as the game draws it
  with both, and the log says which Expand World settings work otherwise on a Riverheim world (the Riverheim page lists
  them all).

## v1.89.0 — 2026-10-20

### Added
- **Riverheim worlds open.** With the Riverheim switch on in Settings, a world made with Riverheim is drawn with your own
  copy of the mod: its ground, biomes, forests, map and predictions are Riverheim's, as the game makes them.

## v1.88.0 — 2026-10-20

### Added
- **Worlds made with Riverheim are recognised.** The worlds list tags them and says why they cannot be opened yet
  (turn Riverheim on in Settings, install Riverheim); before, their name and seed were read wrongly.

## v1.87.4 — 2026-10-21

### Fixed
- **The Deep North's ground matches the game to the bit.** A few points of the Deep North came out a hair higher or
  lower than the game makes them, with or without Expand World.

## v1.87.3 — 2026-10-20

### Fixed
- **Expand World Size's wider or stretched worlds match the game near the Deep North.** The ground along the Deep
  North's edge came out a hair higher or lower than the game makes it.

## v1.87.2 — 2026-10-20

### Fixed
- **Expand World Size's "Stretch biomes" lays the biomes out as the game does.** The editor spread the biome fields
  from the wrong origin, so about one point in fifteen had a different biome than in the game.

## v1.87.1 — 2026-10-20

### Fixed
- **Expand World Size's altitude and water depth match the game to the bit.** With those settings changed, about one
  point in eight came out a hair higher or lower than the game makes it.

## v1.87.0 — 2026-10-20

### Added
- **Pick only built pieces, or only nature.** The Select panel's Pick choice makes clicks, boxes and zones take only
  the pieces players built (a box over a house leaves its trees and rocks) or only trees, rocks and the like; what is
  left out is not in the way of what is behind it.

## v1.86.0 — 2026-10-20

### Added
- **Click again to reach what is behind.** A second click at the same spot selects the next object behind the one
  selected there (a chest, then the floor under it, then the beam below), and round again, as in Unity.

## v1.85.0 — 2026-10-20

### Changed
- **The selection is outlined like in Blender.** An orange line follows the shape of the selected objects, seen
  through what is in front of them, instead of boxes; the last object clicked is outlined in a lighter yellow.

## v1.84.0 — 2026-10-20

### Added
- **Selection keys from Blender.** Ctrl + A selects every shown object, Alt + A clears the selection, Ctrl + I
  inverts it, Ctrl + H hides the selected objects in the view to reach what they cover (Alt + H shows them again;
  nothing changes in the world), and Shift + F or Numpad . turns the camera onto the selection. The Select panel has
  All, Hide and Show hidden buttons. The camera no longer moves when Ctrl or Alt is held with a movement key.

## v1.83.0 — 2026-10-20

### Changed
- **Selection keys like Blender or a map editor.** Ctrl + click adds an object, or takes it out when it is selected;
  Shift + click takes it out; Ctrl + drag adds what the box takes, Shift + drag takes it out. The row from the last
  object clicked is now Ctrl + Shift + click.

## v1.82.0 — 2026-10-20

### Added
- **Select through.** A new Select panel switch (Alt + Z) makes the box on the screen also take what is hidden behind
  others inside it, like Blender's X-ray: the furniture inside a house, the wall behind another.

## v1.81.0 — 2026-10-20

### Added
- **A drag selects what is seen inside a box on the screen.** Like in Blender or a map editor, a drag on empty ground
  (or Alt + drag over a building) draws a box and selects only the objects seen inside it, not those behind others,
  so you can take one floor, one wall or a roof instead of everything from the ground up. The Select panel's "A drag
  draws" brings back the zone on the ground.

## v1.80.0 — 2026-10-20

### Changed
- **One thin box marks a selection of several objects.** The 3D view drew a box round each selected object, a tangle
  of lines over a building; it now draws one box round them all, with thinner lines, and selected objects lose their
  unsaved green dot until you select something else.

## v1.79.2 — 2026-10-20

### Fixed
- **Objects a mod hides deep under the ground are no longer picked with what stands above them.** A zone drawn over a
  house took the crafting stations a mod keeps 10 km under it, and moving the house with "Put each object on the
  ground when moving" on then lifted it 10 km to put them on the ground. Zones, Same kind, Invert, Whole building and
  the Area tool now leave out anything more than 10 m under the ground.

## v1.79.1 — 2026-10-20

### Fixed
- **A building moved with "Put each object on the ground when moving" off keeps its shape.** Each object kept its own
  height above the ground under it, so on uneven ground the pieces went up and down apart and the building came out
  crooked. The whole selection now moves as one block, up or down with the ground under its middle.

## v1.79.0 — 2026-10-20

### Added
- **The Dungeon tool builds Expand World Data's blueprint rooms.** A room of the room file made of a blueprint joins
  dungeons with its openings where the blueprint's snap points are, and brings the blueprint's pieces with it.

## v1.78.0 — 2026-10-20

### Added
- **The Dungeon tool joins each room only as far from the entrance as the game allows.** The game's rooms (and Expand
  World Data's room file) say how many rooms from the entrance a room may appear at the fewest; shrines, treasure
  rooms and deep halls no longer turn up right at the way in.

## v1.77.0 — 2026-10-20

### Added
- **The Dungeon tool follows Expand World Data's object swaps and data for rooms and dungeons.** A room's objects are
  swapped, removed or given data as the room and dungeon files say, and the objects a room file adds keep their data.

## v1.76.0 — 2026-10-20

### Changed
- **New world and Regrow nature follow the game's random draws more closely.** Objects that make random choices as
  the game creates them (chests filling with loot, beehives, fires, creatures, ruins and others) are now taken into
  account, so Expand World Data's swaps, data and added objects, and the vegetation after such objects, come out as
  the game makes them.

## v1.75.1 — 2026-10-20

### Fixed
- **Location objects the game drops onto the ground stand on it in the zones the editor generates.** The start
  temple's stones and branches, the tar pits' lox ribs, the ship settings' rocks and treasure, the traders and
  others were left at their height in the location instead of on the ground beneath.

## v1.75.0 — 2026-10-20

### Added
- **Alt biomes' ground look.** An Expand World Data alt biome that gives its areas another biome's ground texture
  ("terrainTextureOverride") is drawn that way in the 3D view, as the game draws it.

## v1.74.1 — 2026-10-20

### Fixed
- **Expand World Data's location and alt biome files are used for a world opened in the editor.** They were skipped
  when the world opened before the game's own location rules had been read, so the map and the 3D view showed the
  game's locations and alt biomes.

## v1.74.0 — 2026-10-20

### Added
- **New world builds blueprint locations with a terrain snapshot.** Their ground takes the heights and paint the
  blueprint captured, in every zone the snapshot reaches, as Expand World Data does, where such a location used to be
  left for the game to build.

## v1.73.0 — 2026-10-20

### Added
- **The Dungeon tool follows Expand World Data's room and dungeon files.** Rooms turned off, changed or copied
  under a new name, and each dungeon's themes, size, room counts, required and excluded rooms, room limits and
  doors, are used when it makes a dungeon from the game's rooms and in its list of rooms to add.

## v1.72.0 — 2026-10-20

### Added
- **The map's pointer box tells more.** It names the alt biome of the area under the pointer (Dark Meadows, Bog
  Swamp...), and with Expand World Data its territory and the status effects a player gets there.

## v1.71.0 — 2026-10-20

### Added
- **Expand World Data's water colours.** With its "Custom water color" setting on, the 3D view's water takes the
  colours the biome or the territory under the camera gives, as the game colours the water where the player is.

## v1.70.0 — 2026-10-20

### Added
- **The map shows the locations' icons.** The start and the traders are marked on the world map with the game's own
  icons, as in the game; with Expand World Data, the icons its location file gives.

## v1.69.0 — 2026-10-19

### Added
- **The map draws the sea at the world's real water level.** With Expand World Rivers moving the water, the coast
  on the map now follows it, instead of the game map's fixed line.

## v1.68.0 — 2026-10-19

### Added
- **New world builds Expand World's blueprint locations.** A location made of a blueprint is now built in the
  generated zones as the mod spawns it, with its pieces, the blueprints it holds, the file's swaps and data, and its
  levelled ground, instead of its zone being left to the game. Blueprints with a terrain snapshot are still left to it.

## v1.67.3 — 2026-10-19

### Fixed
- **Expand World blueprint locations are laid out at the mod's size.** The room a blueprint location needs is now
  measured as the mod measures it (a blueprint in a subfolder or with spaces in its name is found, and its terrain
  snapshot counts), and live mode reads every blueprint of the game's folder, so the predicted places match the game's.

## v1.67.2 — 2026-10-19

### Fixed
- **Odd Expand World files no longer stop the editor.** A world too large or stretched too far for a biome map is
  refused with a message, a blueprint holding the blueprint it is in is left out instead of being grown without end,
  and choices whose weights add to nothing, or a vegetation check with no rings, are handled as the mod handles them.

## v1.67.1 — 2026-10-19

### Fixed
- **Expand World changes the worlds only where the mods are installed and on.** With the switch on, a mod that was
  removed or turned off in a mod manager no longer counts because its settings files were left behind, and a world
  made with Expand World Size is generated as the game generates it when no folder has the mod.

## v1.67.0 — 2026-10-19

### Added
- **New world builds Expand World locations that level or paint their ground.** The ground round such a location
  is now levelled and painted in the generated zones as the mod does it, and its objects and the vegetation stand
  on it, instead of the zone being left to the game.

## v1.66.2 — 2026-10-19

### Fixed
- **Ground painted by Expand World Data shows and edits in the editor.** Terrain the mod levels or paints round a
  location was skipped as damaged, so those zones showed as never edited; they are now read as the game reads them.

## v1.66.1 — 2026-10-19

### Fixed
- **New world's locations stand on the ground as the game spawns them.** A location in the generated zones now
  stands on the terrain under its spot, as the game puts it, instead of slightly above or below it, so its objects
  are at the game's heights.

## v1.66.0 — 2026-10-19

### Added
- **Expand World vegetation with data and blueprints grows as the mod grows it.** Regrow nature and New world's
  generated zones now give each object its rule's "data" and "drops", and grow a blueprint named as a rule's prefab
  as its pieces, each made by chance, turned and scaled as the mod does it.

## v1.65.0 — 2026-10-19

### Added
- **New world wears damaged locations as the game does.** Building pieces of ruins and houses the game spawns worn
  now get the game's random health in the generated zones, and Expand World locations with "randomDamage" are built
  by the editor instead of being left to the game.

## v1.64.1 — 2026-10-19

### Fixed
- **New world writes Expand World's cloned locations as the mod does.** A copy of a game location ("Name:Tag") in
  the generated zones now spawns the game location it copies, with its own name kept as the mod keeps it, instead of
  a name the game has no location of.

## v1.64.0 — 2026-10-19

### Added
- **New world builds the Expand World locations whose contents the mod changes.** Objects the location file adds,
  objects it swaps, removes or gives data, and a location's own data, scale, height and cardinal turn are now made
  in the generated zones as the mod makes them, instead of those zones being left to the game. Locations the editor
  cannot build ahead of the game (commands, levelled ground, random damage, data worked out as the game spawns) are
  still left to it.

## v1.63.0 — 2026-10-19

### Added
- **Expand World vegetation that wants the greenest spots grows where the game grows it.** A vegetation rule of the
  mod's file that checks the vegetation round each spot now does so in Regrow nature and New world as the game
  does: the first spots of a zone set the bar, and only spots that reach it grow.

## v1.62.0 — 2026-10-19

### Changed
- **Regrow nature and New world keep the ground clear round locations as the game does.** Nothing grows in the
  square the game keeps clear round a village, a dungeon entrance or any place whose kind clears its ground, and
  the trees and rocks after it fall where the game puts them. With Expand World, the location file's clear areas,
  a wide place in the next zone and the vegetation file's clear areas are followed too.

## v1.61.0 — 2026-10-19

### Added
- **Alt biomes are where the game puts them.** The editor now works out which areas get an alt biome (Dark Meadows,
  Rock Black Forest, Goblin Plains...) from the seed, as the game does. The New world page shows the dungeons an
  alt biome adds or keeps out where the game puts them, and Regrow nature and New world's zones grow what an alt
  biome adds (mushrooms, fireflies, ruined walls...) and leave out what it keeps out. With Expand World, its alt
  biome file and the alt biomes its world file forces onto areas are followed too.

## v1.60.3 — 2026-10-19

### Fixed
- **Locations are predicted where Expand World Size puts them on a bigger or stretched world.** The mod sizes the
  game's biome map to the world, and locations are drawn from that map; the editor kept the game's map, so the New
  world page, the dungeon entrances and New world's zones put locations in the wrong places on such worlds.

## v1.60.2 — 2026-10-19

### Fixed
- **Expand World vegetation scaled by axis keeps its shape.** Trees and rocks that Expand World Data stretches by axis
  were saved with the same scale on every axis by Regrow nature and New world; each axis now keeps its own.

## v1.60.1 — 2026-10-19

### Fixed
- **Expand World vegetation rules that need a global key follow the world's keys again.** Expand World Data 1.79
  renamed the vegetation file's global key fields, so such rules were always grown; both names are now read.

## v1.60.0 — 2026-10-19

### Changed
- **Moved objects land on what is under them, not only on the ground.** With the Select tool's **Put each object on
  the ground when moving** on, a chair, a chest, a creature or any other object you move now ends up on the top of
  the floor, stair, table or other object under it where you let go, and on the ground only when nothing is under
  it, as `End` has always dropped it. Each object is carried at the height above the ground it had, so a top higher
  than the object itself (a roof, a wall) does not count: it goes under. A building still moves as one block that
  keeps its shape, its bottom layer put on the floor or platform under it, else on the ground. Before, every moved
  object was set onto the ground, through the floor it was moved onto.
  
## v1.59.0 — 2026-10-19

### Added
- **Creatures are never sunk into building pieces.** A creature put down, moved, copied, pasted, placed by a script or
  brought by a blueprint with its feet inside a floor, a wall or any other building piece now stands on top of the
  highest piece under it instead; the ground is left as it was (a creature stands on it as placed). The message of
  each step says how many were lifted (`2 creature(s) stood on the piece under them.`), and Claude's answers give
  their final height. The Workshop's support check also counts the creatures still inside a piece (a blueprint from
  another tool can have them) in its bar and in Claude's `support_check`.

## v1.58.0 — 2026-10-19

### Added
- **Blueprints say which pieces only the editor can place, and cost only what the hammer builds.** Homestead's
  hammer builds the real build pieces of a blueprint and silently skips the rest (statues, ships, creatures, the
  T.W.I.G.); only the editor places those, into a world or live. A blueprint the editor saves now marks them in its
  file (lines Homestead skips), the library and Claude's `list_blueprints` say how many (`2 of 40 piece(s) only the
  editor can place`), and the cost shown for a blueprint (library, Build panel, the Workshop's title and save dialog,
  `list_blueprints`) leaves them out, so it is what a hammer user pays. Blueprints from before, or Homestead's own,
  carry no marks and say nothing rather than a wrong zero.

## v1.57.5 — 2026-10-19

### Fixed
- **A bed on a floor can be clicked again.** A click on a bed, a hide rug or any other piece drawn with a cut-out
  material went through it to the floor piece under it, as it does through a tree's leaves. Only leaves, bushes and
  grass are looked through now; a cut-out piece is picked where its shape is, like any other.

## v1.57.4 — 2026-10-19

### Fixed
- **Opening the Workshop while a live world is open no longer leaves the map asking the game for its players.** The
  world's connection was closed with the world, but the map kept asking it every few seconds, and the log filled with
  "Cannot access a disposed object" lines until the editor was closed or another world opened.

## v1.57.3 — 2026-10-19

### Fixed
- **Live, the editor shows every object where the game really has it, its own included.** An object you placed and
  applied kept the place and data the editor sent: the game had since set the object up (a creature's health, its
  spawn point and look) and physics had settled it, and the editor, Claude's `get_object` and `list_objects` still
  showed the editor's copy. Creatures and other things that move on their own, and objects another mod changes, were
  followed only when they appeared or went. Now, once applied, your objects take the place, turn and data the game
  gives them (under the same id: undo still takes them away, and a copy is made from the game's version), and
  creatures, ships, carts, items, fires and the like are followed as they move or change, read alone so a creature
  walking does not have its whole zone read at every look. Needs WorldEditorBridge 1.57.3 for the moves; with an
  older plugin, your applied objects still take the game's data when something else in their zone changes.

### WorldEditorBridge
- `POST /watch` gives a second number per zone, which changes when a kind that moves or ticks on its own (creatures,
  ships, carts, items, fires, smelters...) moves or changes its data; the first number is as before.
- `POST /zone/movers` serves only those kinds of the zones asked for, in `/zone`'s format.

## v1.57.2 — 2026-10-19

### Fixed
- **No limit ground loads before the buildings on it, not disc by disc among them.** A player's game spawns a
  zone's objects a few per frame, nearest first, and every ground disc rebuilt the zone's ground as it came in: on
  a big build the ground climbed for ten seconds or more at every visit, with the game stuttering. Discs are now
  saved as objects of the Terrain kind, which the game spawns before everything else, so the ground is finished in
  the first frames and the pieces then come in on top of it. Discs saved before this version are changed by the
  WorldEditorBridge plugin (below) or when their ground is lifted again.

### WorldEditorBridge
- **Ground discs are kept first in line.** The game puts a disc back to the Solid kind when the player who owns it
  spawns it; the plugin looks at the world's discs every few seconds, puts them back to Terrain, and changes the
  discs of older editor versions at the first look. The log says how many discs the world has and when any were
  put back.

## v1.57.1 — 2026-10-19

### Fixed
- **A live world's settings from Expand World are read again.** The editor asks the game's WorldEditorBridge for
  the world's file (the mod keeps the world's size there), and the plugin looked for it where older games saved it
  (`worlds/name.fwl`): the game now saves `worlds/name/_main.n.fwl2`, so the plugin failed, and the editor then
  read nothing through the bridge and fell back to the BepInEx folders on this computer. The plugin now serves the
  file the game saves (or the older one for a world not saved by this game yet), and the editor still reads the
  mods' files through the bridge when only the world's file cannot be served.

### WorldEditorBridge
- `GET /world/meta` serves the world file the game saves (`_main.n.fwl2`), else the older `.fwl`; 404 when the
  game has neither.

## v1.57.0 — 2026-10-19

### Added
- **The history keeps every change by default, and you choose how many in Settings.** It used to keep the last 200
  changes only. **Settings > History > Steps kept** sets a number; empty or 0 keeps them all, as long as they take
  less than 2 GB of memory (past that, the oldest go first). The History panel shows the newest 200 changes, and
  **Show older** adds 200 more each time.

## v1.56.1 — 2026-10-19

### Fixed
- **The Workshop's support check no longer counts the parts of a piece that move on their own.** The swinging leaf
  of a door, a gate, a drawbridge or a window's shutter, and the hulls of two ships, held up whatever rested on
  them, where the game takes no support from them: a pole leaning on a door's leaf now falls, as it would in game.

## v1.56.0 — 2026-10-19

### Added
- **The 3D view draws what item stands and armour stands hold.** An item stand shows its item hanging or lying as
  the game shows it (a shield on the wall, a trophy, food on a table), an armour stand its armour, helmet, cape and
  the weapons in its hands and on its back. They follow the stand (shown and hidden with its kind, moved with it)
  and change at once when the inspector or Claude changes what the stand holds. Shields and capes are drawn in
  their first style, and an armour stand in its resting pose. Items on stands cannot be picked: pick the stand.

## v1.55.2 — 2026-10-19

### Fixed
- **On Windows the editor no longer holds on to the YamlDotNet library in your BepInEx plugins folder.** The file was
  kept open as long as the editor ran, so a mod manager could not update it; now the editor reads it once and lets
  it go.

## v1.55.1 — 2026-10-19

### Fixed
- **Expand World Data's alt biome file counts even without a location file.** What each alt biome keeps out was
  only used when the location file was in use or Expand World Size changed the game's kinds; now the game's own
  kinds take it into account too.

## v1.55.0 — 2026-10-19

### Added
- **The New world page's map shows the Expand World mods' own colours** (with the Expand World switch in Settings on):
  a biome of Expand World Data's biome file is drawn in the map colour the file gives it rather than in the colour of
  the ground it borrows, a territory in its own, and the water line on that map follows Expand World Rivers' sea
  level. The same holds for the closer look when zoomed in and for the seed pictures Claude's tools give.

## v1.54.0 — 2026-10-19

### Added
- **A live world follows the Expand World mods the game itself runs** (with the Expand World switch in Settings on):
  the editor reads the mods' settings and data files from the game's own BepInEx folder through WorldEditorBridge,
  along with the world's own settings from its file on the game's disk, instead of the BepInEx folders on this
  computer. The YamlDotNet library the data files need is still taken from a local BepInEx folder. An older plugin
  leaves the local folders in use, said in the log. See [Expand World](addons/ExpandWorld/README.md#live-mode).

### WorldEditorBridge
- Three read-only routes for the editor: `GET /plugins` lists the plugins BepInEx loaded (id, name, version),
  `GET /config?path=` serves a file under `BepInEx/config` or lists a folder there (nothing outside it, 4 MB at most
  per file), and `GET /world/meta` serves the world's meta file as it lies on disk. The status lists the feature
  "files".

## v1.53.0 — 2026-10-19

### Added
- **The map, the New world page and the 3D view follow the Expand World mods** (with the Expand World switch in
  Settings on): the overview covers a wider world, the map opens on all of it, a seed is looked at and a new world
  made with the mods' settings and data (the page says so above Create), biomes the mods add are named and coloured
  as their files say, and a world made with Expand World Size shows "Expand World" on its card on the start page.
  Settings gets a folder for the mods' files, for a server world opened from its files. See
  [Expand World](addons/ExpandWorld/README.md).

## v1.52.0 — 2026-10-19

### Added
- **Worlds made with Expand World Data's locations and vegetation are laid out as the mod lays them out**, with the
  Expand World switch in Settings on: the location file (clones of the game's locations, blueprint locations, groups,
  "away from" and "close to" rules, a location wider than a zone put at the zone's middle, the start temple forced into
  the middle zone when none was placed), the vegetation file (several prefabs drawn one at a time, a scale by axis, a
  centre of its own, rules turned on or off by the world's global keys), the alt biome file (what each keeps out), and
  Expand World Size's locations multiplier. Zone spawners can be left out and a random-seed location is left to the
  game. See [Expand World](docs/architecture.md#what-a-world-generation-mod-changes-worldrules).

## v1.51.0 — 2026-10-19

### Added
- **Worlds made with Expand World Data's biomes are generated as the mod makes them**, with the Expand World switch
  in Settings on: the biome file (new biomes and changed ones, their ground, colours, lava and height transforms),
  the world file (the rules that lay the biomes out, including the older "legacy" evaluation, sectors, rectangles and
  the boiling sea) and the territory file. The editor reads the mod's files through the YamlDotNet library the mod
  installs under the BepInEx plugins folder; it ships no copy of its own. The location, vegetation and alt biome
  files come in a later step. See [Expand World](docs/architecture.md#what-a-world-generation-mod-changes-worldrules).

## v1.50.0 — 2026-10-19

### Added
- **Worlds saved on a wider zone grid open and save as they lie.** Expand World Size lays a world wider than 16 km
  out on a bigger grid of chunk files (up to 2048 zones a side, about 65 km) and notes the grid in the world file;
  the editor now reads that note and files every object and zone under the right chunk, whether or not the Expand
  World switch is on. Before, anything past 16 km was taken for outside the world. See
  [Expand World](docs/architecture.md#what-a-world-generation-mod-changes-worldrules).

## v1.49.0 — 2026-10-19

### Added
- **Worlds made with Expand World Size and Expand World Rivers are generated as those mods make them** (the first
  step of the editor's Expand World support), once the **Expand World** switch in Settings is on. The world's
  radius and edge, its stretch, altitude and water depth settings, the Ashlands' limits and gaps, the forest
  amount, the seeds the mods replace, the water level and every lake, river and stream setting come from the
  world file (Expand World Size keeps its settings in it) and the mods' settings files in the BepInEx folder.
  With the switch off, as before, every world is generated as the game itself does. Not yet: Expand World Data's
  biomes, locations and vegetation, worlds wider than the game's save grid, and the map's size.
## v1.48.2 — 2026-10-19

### Fixed
- **A new world is saved where the installed game looks for it.** On Linux, the New world page offered the native
  game's folder first even when Valheim is the Windows build run through Proton, which keeps its worlds inside its
  Steam prefix: a world made there never showed in the game. The installed game's own folder now comes first in
  **Saved in** (and is the one Claude's tools use), the others after it.

## v1.48.1 — 2026-10-19

### Fixed
- **The `--export-locations` and `--export-test-data` commands close the game's files when they are done.** They
  kept them open until the editor ended, which on Windows stopped anything else from touching the game's folder
  meanwhile.

## v1.48.0 — 2026-10-17

### Added
- **A double click on an object in the View tool switches to Select with it selected.** Its arrows and ring show at
  once, ready to move or turn it, without pressing `E` and clicking it again. Only a plain double click on an object
  does it: with `Ctrl` or `Shift` held, or on empty ground, View stays. See [Select](docs/select.md#use-it).

## v1.47.36 — 2026-10-19

### Fixed
- **Apply live no longer fails now and then with "An error occurred while sending the request".** After about ten
  seconds without talking to the game, the editor could still send on a connection the game was closing at that
  moment; it now always opens a new one by then.

## v1.47.35 — 2026-10-19

### Fixed
- **The game data's reading no longer keeps open a file in the bundles folder that is not a bundle.** The pass over
  the game's bundles at the editor's start passed such a file over but kept it open, as the model reading did before.

## v1.47.34 — 2026-10-19

### Fixed
- **A file in the game's or a mod's bundles folder that is not a bundle is no longer kept open.** The editor passed
  such a file over but kept it open; on Windows that stopped the mods' copies from being replaced when a mod changed.

## v1.47.33 — 2026-10-19

### Fixed
- **A save on Windows no longer fails while another program holds the file for a moment.** The settings, saved
  servers, blueprints and caches are written beside the old file and moved over it; when Windows refused the move for
  an instant (an antivirus scanning the file, two saves at once), the save failed with "Access to the path is denied",
  and it is now tried again for up to half a second.

## v1.47.32 — 2026-10-19

### Fixed
- Internal code cleanup; no change for users.

## v1.47.31 — 2026-10-17

### Fixed
- **The Dungeon panel no longer fails at the editor's start.** It made its first dungeon a moment after the window
  opened, before the game's data was read, so no item was known yet and the key chest failed ("“CryptKey” is not an
  item of the game"). It now says it waits for the game's data and makes the dungeon once the data is in use.

## v1.47.30 — 2026-10-19

### Fixed
- Internal code cleanup; no change for users.

## v1.47.29 — 2026-10-17

### Fixed
- Internal code cleanup; no change for users.

## v1.47.28 — 2026-10-17

### Fixed
- Internal code cleanup; no change for users.

## v1.47.27 — 2026-10-17

### Fixed
- **Errors the editor caught are now written to the log.** Many failures (a file that cannot be read, a game bundle that cannot be opened, a bridge that does not answer, a model that cannot be loaded) used to pass without a word. Every one now adds a line to `ValheimWorldEditor.log` saying where it happened, what was being done and the error, with the stack trace unless the failure is an expected one; a failure that repeats every few seconds is logged only when it changes, and a cancellation you asked for is a plain line, not an error.
- **Personal data is removed from the log.** The home folder is written `~`, and file paths, web addresses, user@host names, IP addresses other than the local one, host names before a port, your account's user name, and saved servers' addresses, accounts, passwords and tokens become placeholders. Crashes and errors nothing else caught are cleaned the same way.

### WorldEditorBridge
- **Errors the plugin caught are now all in BepInEx's log.** A listener that could not be stopped, an error answer that could not be sent and an unreadable mod manifest.json used to be passed over in silence. Each is now logged as `WorldEditorBridge: caught at <file>:<line> (<member>) while <doing>: <type>: <message>`, with its stack trace unless it is an expected one and the server's home folder written `~`.

## v1.47.26 — 2026-10-17

### Fixed
- Internal code cleanup; no change for users.

## v1.47.25 — 2026-10-17

### Fixed
- **Loading a stamp picture that could not be opened no longer stops the editor.** This happened with a picture deleted or moved after you chose it in Load stamp…, a folder, a file the editor may not read, one in use, or one too large to decode. The status bar now says "That picture could not be read.", as for a file that is not a picture, and the log says what failed (without the file's path); files over 256 MB, and pictures that would take more than that once decoded, are not read.

## v1.47.24 — 2026-10-17

### Changed
- **The Inspector now follows the language chosen in Settings.** Its buttons, headings, explanations and messages, the plain names of the most common values (Text, Builder name, Contents…), the kinds of values you can add, the kinds of items an armour stand's slots take, why a stand refuses an item, and its question about items outside a container's slots are translated. Names of objects, items and values, and the tips made from them, stay as the save has them; in English nothing changes.

## v1.47.23 — 2026-10-17

### Changed
- **What the status bar and History say about Claude's changes now follows the language chosen in Settings.** This covers the message after each Claude change ("Claude moved 3 object(s); Ctrl+Z puts them back.") and the names of Claude's steps in History, which keep their "Claude: " mark so Take back Claude's changes still finds them. In English nothing changes, and Claude is still answered in English.

## v1.47.22 — 2026-10-17

### Changed
- **The start page now follows the language chosen in Settings.** This covers its menu, Home's tiles, My game's state and install steps, the saved servers and the server form, the saved worlds, the Workshop's blueprints, Homestead's state, and the card about reading the game's look. In English nothing changes.

## v1.47.21 — 2026-10-17

### Changed
- **The New world page now follows the language chosen in Settings.** This covers its boxes and buttons, the search's wishes and results, the lists beside the map (the world, biomes, traders, bosses and dungeons with their distances), the switches and tooltips over the map, why a name or seed is refused, and the messages while the world is made. Claude is still told in English; in English nothing changes.

## v1.47.20 — 2026-10-17

### Changed
- **The world map page now follows the language chosen in Settings.** This covers its buttons and switches, what is waiting to be saved, the live players' line, the picked spot, the search and its result counts, the zone filter with its messages and question, the edited zones, the map's status line and the biome names. In English nothing changes; the world's and objects' names stay as they are.

## v1.47.19 — 2026-10-17

### Changed
- **The Dungeon tool and its generator now follow the language chosen in Settings.** This covers the panel's buttons, lists and lines, the generator's settings and its summary, the names of the game's dungeon kinds and entrances, and the status bar and History lines for dungeon edits and placed dungeons. In English nothing changes, except that a room added with two or more doors now says how many instead of repeating "and a door" for each; names of rooms, biomes, styles, materials and bosses stay as the game and generator call them, and Claude is still told in English.

## v1.47.18 — 2026-10-17

### Changed
- **The Area tool now follows the language chosen in Settings.** This covers its panel (the selection, every action with its settings and hints, the Replace, heightmap, backup and Reset zones rows), the clipboard line, the Reset zones question, its file pickers, status messages and History steps. Claude still gets the actions' names in English; in English nothing changes.

## v1.47.17 — 2026-10-17

### Changed
- **The Place tool now follows the language chosen in Settings.** This covers its panel (modes, line shapes, presets, chosen kinds and their notes, every setting and hint), the list of kinds and what a spawn point spawns, the preset questions, status messages and History steps. In English nothing changes.

## v1.47.16 — 2026-10-17

### Changed
- **The Workshop and its Build panel now follow the language chosen in Settings.** This covers the Build panel's tabs, search, snap, grid, turn and lift rows and Library, and the Workshop's title, cost line, save bar, support switch and cut, its questions before leaving an unsaved building or world, and its status messages and undo steps. Claude is still told in English; in English nothing changes.

## v1.47.15 — 2026-10-17

### Changed
- **The Blueprints panel now follows the language chosen in Settings.** This covers its buttons, the Homestead banner, each blueprint's line and cost, its questions before replacing or deleting a blueprint, the import file picker and every status bar message. Claude is still told in English; in English nothing changes.

## v1.47.14 — 2026-10-17

### Changed
- **The View panel, the help card and the status bar's readouts now follow the language chosen in Settings.** This covers the View panel's switches, kinds of objects, overlays, 3D resolutions and headings, the help card's controls, the frame rate, the selection and walking or flying lines, picking a kind and the paste height. In English nothing changes.

## v1.47.13 — 2026-10-17

### Changed
- **The 3D editor's area bar and the live game's messages now follow the language chosen in Settings.** This covers the area's arrows, size, Follow, Discard, Reload and Auto, their questions and messages, the locations note, the live players and what came from the game, the area's line under its name, how it loaded, and the window title on the New world page. In English nothing changes.

## v1.47.12 — 2026-10-17

### Changed
- **The Select and Paste tools now follow the language chosen in Settings.** This covers the Select panel (its buttons, exact place and saved selections), the Paste panel, the messages for selecting, moving, dropping, deleting, copying, cutting and pasting, Make player built and the Built by choices, and the questions they ask. In English nothing changes; the name a mod gives its linked objects ("vehicle") stays as the mod wrote it.

## v1.47.11 — 2026-10-17

### Changed
- **The Shape and Mountain tools now follow the language chosen in Settings.** This covers their panels (presets, rows, help lines, a formula's mistakes), the status messages for putting a shape or a mountain, and their History steps. In English nothing changes; scripts still name a mountain preset in English.

## v1.47.10 — 2026-10-17

### Changed
- **The Path and Cave tools now follow the language chosen in Settings.** This covers the Path panel's actions, rows, cave presets and roofs, the line's length, and what Apply says along with its History step. In English nothing changes; Claude's dig_path is still answered in English.

## v1.47.9 — 2026-10-17

### Changed
- **The Measure, Mask and Script tools and the biome names now follow the language chosen in Settings.** This covers the tape's hints and measured values, the Mask's rows, paint rules and warnings, the Script panel with its question before replacing a saved script and what a run says, and the biome names in the Mask and the map's zone filter. In English nothing changes; Claude still names biomes, and is told about masks and scripts, in English.

### Fixed
- **A Run of the Script tool's first example was named "Script: script" in History.** The example shown when the panel opens now counts as chosen, so the step is named after it.

## v1.47.8 — 2026-10-17

### Changed
- **The brush options now follow the language chosen in Settings.** This covers their rows, shapes, falloffs and stamps, the brushes' messages (No limit, stamps, Alt + click), the Build label of the Workshop's rail and the cursor readout in the status bar. A built-in stamp chosen as the brush's shape is still remembered after the language changes; Claude still asks for stamps by their English names, and in English nothing changes.

## v1.47.7 — 2026-10-17

### Changed
- **The History panel and the save bar now follow the language chosen in Settings.** This covers the History card with its rows and buttons, what is waiting to be saved, the Save to world and Apply live buttons, what saving, applying, discarding, leaving and quitting ask and say, and the brush's and Regrow's messages about what could not be done. In English nothing changes.

## v1.47.6 — 2026-10-17

### Changed
- **The small dialogs and the words they share now follow the language chosen in Settings.** This covers questions, messages, the line of text and a blueprint's details, the OK, Yes, Cancel, Stay, Keep them, Leave anyway, Keep building and Discard buttons, and the file tooltip of a mod's extension in Settings. In English nothing changes.
- Claude is still told the status bar's messages in English whatever language the window shows.
- A remembered list choice (the brush's shape and falloff, the Area action, the Shape preset, the 3D resolution) is kept in English, so it still comes back after those lists are translated and the language is changed; choices kept so far are already English.

## v1.47.5 — 2026-10-17

### Changed
- Internal code cleanup; no change for users.

## v1.47.4 — 2026-10-17

### Fixed
- **The release packages could not be built.** Publishing the single-file editor stopped on an error in how scripts find the editor; it is fixed, and the published editor starts its scripts as before.

## v1.47.3 — 2026-10-17

### Changed
- Internal code cleanup; no change for users.

## v1.47.2 — 2026-10-17

### Changed
- Internal code cleanup; no change for users.

## v1.47.1 — 2026-10-17

### Changed
- Internal code cleanup; no change for users.

## v1.47.0 — 2026-10-17

### Added
- **Your changes are kept when the game saved the world since you opened it.** Save used to be refused then, and the only way out was to leave the world and lose every unsaved change. The editor now offers to bring them over: it reads the game's save and makes each pending change again on it (changed ground, zone resets, No limit ground, deleted, added and moved objects with their data). Where the game changed the same zone or object, it asks once per kind whose version stays; the world is then the game's save with your changes pending and your history kept, and nothing is written until you save. See [Saving](docs/concepts/saving-and-history.md#the-game-saved-since-you-opened-it).

## v1.45.110 — 2026-10-17

### Fixed
- **Edits to a world saved by Valheim 1.0.20 can be saved.** The game now saves worlds in save format 42, and the editor refused to save any world not in format 41. It now saves both, each file keeping the format it was read in as the game does; new worlds are made in format 42, as the game makes them, and Valheim 1.0.17 and earlier cannot open them.

## v1.45.109 — 2026-10-17

### Fixed
- **A changed server key now names every saved entry still holding the old key.** The refusal told you to empty one HostKey line, but another saved account on the same server and port also pins the key, so the connection stayed refused. The message now lists each saved entry to empty.
- **A worldeditor.json lying directly in BepInEx/plugins no longer offers an extension named "plugins".** Such a file belongs to no mod, yet it could point at any mod's DLL. Its data and prefab notes are still read; extensions come only from a mod's own folder.
- **A line break in a server's details can no longer add lines to servers.cfg.** A token or folder holding a line break followed by "HostKey = …" could become a line of its own and replace the saved server key. Connecting now refuses details with line breaks, a name's line break becomes a space in the file, and ports out of range read as the defaults.
- **A live server's zone answer can no longer make the editor use unbounded memory.** A misbehaving service on the tunnelled port could fill memory; an answer larger than a whole world snapshot is now refused.
- **Claude can no longer make a world outside the game's worlds folders.** create_world used to write world files in any folder the editor could write to; a folder that is not one of the game's worlds folders is now refused.
- **A damaged history file no longer makes opening a world take gigabytes of memory.** Entries are now kept as they are read, and a negative count is refused.
- **The editor's language now follows the system's display language on Windows and macOS.** Before, English Windows with French regional formats would start in French once a French catalogue exists, and apps opened from Finder on macOS always started in English.
- **A failed plugin check no longer turns a live move into a destroyed and remade object.** A check that failed (a slow answer, say) counted as "cannot move", so a moved chest was destroyed and made again under a new id and ContainerSigns doubled its signs. The plugin is now asked at each Apply live, a failed check stops the apply, and a plugin changed since (the game started again) is noticed.
- **Editing a chest no longer wears the items in it.** About one durability value in five came back a hundredth lower after each save of the chest; it is now rounded.
- **A History action on a step undone meanwhile no longer undoes everything or doubles the change.** While the History panel asked to allow an action, Claude could undo the step or the history drop it, and Back to here then undid every step and Remove applied the change again. These actions now do nothing for a step that is no longer where it was picked.
- **A blueprint piece with blank or zero rotation fields is placed unturned.** Before, such a rotation produced invalid angles in the clipboard and the world.
- **One blueprint file that cannot be read no longer empties the Blueprints list.** The unreadable file is left out (and logged), and the list is quicker to build.
- **Blueprints named after a Windows device (con, nul, com1…) can be saved on Windows.** Their names now start with "_". A Homestead blueprint name cut at 64 characters no longer ends in a dot, which Windows dropped so the picture beside it no longer matched.
- **A world's backups are found when its folder ends in "/" on Windows, and an unreadable folder no longer fails the search.** A folder like "C:/x/worlds/World1/" used to give no backups, and a folder beside the world without read access broke the search; an unreadable folder now has no backups (logged).
- **Characters whose names hold letters beyond ASCII are found.** A character such as "Ørjan" never matched its own file; names are now compared as text, ignoring case.
- **The recent worlds list takes odd stored data and no longer lists a world twice.** A list stored as null or a stored path that is not valid made opening a world fail, and the same folder written with a trailing separator, or with other letter case on Windows, was listed twice.
- **A broken mod bundle no longer fails reading a model or texture, or reads another file's bytes for it.** Bad sizes and ranges past the end of the file are now refused (logged) and read as no data.
- **A broken mod mesh no longer fails drawing a building's picture.** Bad sizes are checked, and triangles with a corner outside the mesh are left out.
- **Following models into other bundles no longer leaves game files open or stops after one bad bundle.** Before, bundles were opened again (on Windows that can block Steam updates) and a bundle that failed midway left later references finding nothing; a bad bundle is now skipped (logged).
- **A shader whose program lengths do not fit together is said to be unreadable instead of failing with an unrelated error.** The message also no longer says "terrain shader" for every shader.
- **A texture array in a format the editor does not read is left out instead of read as another format.** Before, R8 was read as ARGB32 and R8 sRGB as Alpha8.
- **A game bundle that cannot be read is closed at once during the game-data scan.** Before, one that opened but whose inner files failed stayed open until the end of the scan.
- **A mod folder that cannot be read no longer stops the start page checking your game.** Folders without access under BepInEx/plugins are now passed over.
- **A heightmap picture with odd heights written in it no longer breaks its import.** Lowest and highest heights such as NaN, Infinity or 1e39, or a highest not above the lowest, are now left out and the ground's own heights used instead.
- **Two mods' bundles whose names differ only by punctuation or letter case are both drawn.** Before, "Mod A.bundle" and "Mod_A.bundle" (or "Foo" and "foo" in two mods) counted as one and the older one's models were never drawn.
- **Renaming a hand-edited Homestead blueprint shows its new name.** Header lines with spaces before them (" #Name: Old") were not replaced when the name, description or tags changed, so the old line stayed and won.
- **A copy with numbers stored as text can be saved as a Homestead blueprint.** A number written as a string ("1.5") used to make the save fail.
- **One odd line in the game's localisation table no longer stops every world opening.** A carriage return followed by more text in an unquoted field used to fail the build-cost table and with it the whole game data pass.
- **Steam rewriting its library list no longer fails finding the game.** While Steam was writing libraryfolders.vdf (or without access to it) the game check and the game data export failed; the search now goes on without that list (logged).
- **A prefab name found in two game bundles is described by the same object in every catalogue.** The catalogues could describe different objects; all now keep the first.
- **A broken Vulkan shader program is said to be unreadable instead of failing with an unrelated error.**
- **The New world page reads the game's location rules again once the game has run.** Rules that could not be read (the game's log not matching after an update) used to stay empty for the whole session, so bosses and traders stayed missing even after the game refreshed its log; they are now read again when the log changes.
- **Two of the game's location lists with the same counts are both read.** Before, the second was replaced by the first, its locations lost and the first's counted twice.
- **A very large terrain texture array is said to be too large instead of failing with an overflow.**
- **A tunnel that fails after logging in no longer stays open with the start page waiting.** A plugin port out of range in the server's settings, the local port taken in the meantime, or another failure once connected used to leave the connection open (and the port bound) until the editor closed while the page kept saying "Connecting to …". Ports are now checked first, and any failure closes the connection and is said.
- **Proton worlds are listed once, and Flatpak Steam's are found.** Worlds played through Proton were listed twice (on Windows, so were worlds written with other letter case or a trailing separator), and Flatpak Steam's Proton prefix is now searched too.
- **A world with a stray save file is listed, and one that cannot be read is said in the log.** A file such as _main.99999999999.chunks used to drop the world from the list, and every read error did so silently; now only read errors leave a world out, each logged.
- **Mod models taken while a game's catalogue came in could be lost.** Overlapping updates could drop one; they no longer do.
- **A prefab a game update made the game's own no longer shows under Mods or as "From a mod".**
- **Two saves of the same app file at once could fail or keep a half-written one.** Each write now uses its own temporary file, deleted on any failure.
- **Dungeon generation could rarely stop with an error on a random pick.** It now falls back to the last item when rounding leaves a little over.
- **The map's zone filter now counts what was placed this session.** A building placed and not yet saved showed 0 pieces, so a "far from buildings" filter could pick its zone for reset.
- **Linked objects of two mods could join up, and a damaged copy pasted two groups under one id.** Parts now look for what they hang on under their own rule, and each extra holder of a repeated id gets an id of its own.
- **A shader blob could fail to read on a chance "LOMS" in its data.** The search for the next program no longer starts inside the last one's data, and oversized or too-large programs are passed over.
- **Mod models read by several threads at once could fail.** The models are now read once, when taken in.
- **A pit arena's four corner columns never stood.** The columns are now placed first (away from the doorways), then the ledges.
- **Some dungeon stones sank into their plinth or floated over it.** A shrine's memorial stone and dolmen used the wrong prop's foot height (a large memorial stone sank 0.13 m); each now uses its own.
- **Dungeon columns could rise through the roof into the room above.** A column's or post's last segment may now rise into the roof slab, never past it (wood logs in a tall room reached 1.3 m into the room two levels up).
- **Caged foes could walk out of a cell block's end cells.** The first and last cells had no front wall; their fronts now run on to the end walls.
- **The game's-rooms dungeon was placed under another name than the panel showed.** The Generate panel's name now follows the biome for these dungeons, as the placed one does.
- **A portal at a generated dungeon's arrival stood in the entrance's floor.** It is now 5 cm over the entrance's floor finish (marble tiles, boards).
- **Chosen foes and Respawn off were not kept in some dungeon rooms.** Elites were always the biome's, a swamp burial chamber's foes were always sleeping draugr, and an ossuary's bone pile kept raising the dead with Respawn off; chosen foes now replace them, and the bone pile is left out when foes are chosen or nothing respawns.
- **A dungeon of the game's rooms failed with a meaningless error when the game's data had none to join.** It now says the game's data has no rooms to join (a game update may have changed its dungeons).
- **A throne room's war table could lie across the dais' steps or a doorway.** It now stands only where every spot along it is free.
- **A corridor gate could stand on a pilaster's band of wall.** It now keeps off those bands.
- **A Black Forest dungeon's sword-skeleton elite came back as an ordinary skeleton.** It now stands once, as the creature.
- **A dungeon blueprint whose name hashed to the lowest possible number failed to paste, and a negative seed gave portal tags like "dg-123".** Both are fixed.
- **Support was worked out wrong for scaled pieces.** Support distances and angles were taken from the wrong point; the centre of mass is now scaled too.
- **Pointing the hammer from inside a capsule-shaped piece could place a piece at an invalid position.** It now misses, as it does from inside a sphere or a box.
- **Damaged dungeon or location scan data could crash or hang placing rooms.** A part pointing outside the list, or a loop of parents, now ends the walk.
- **A location scan with "teleports": null loaded no location prefab at all.** Anything that is not a list now counts as none.
- **A new world that failed halfway blocked another try in the same folder.** What was written is now taken away on any failure (and the folder, when it was made for it).
- **Connecting with an odd address or a token holding a line break failed with an exception instead of a message.** It now says the address is not valid (only http and https) or that the token cannot be sent.
- **A PNG file with no header was refused as a "0-bit PNG".** The message now says the header is missing; writing a 16-bit grey PNG also refuses what cannot make a valid picture.
- **Lifting ground could treat an object at a half metre as standing on another point than a disc centred on it.** Both now round halves up, as the game does.
- **The save's read-back check missed smoothing and paint changes in zones not edited.** A write bug altering them could pass and the old save be deleted; they are now compared too.
- **Resetting the ground of a zone that held two terrain objects could refuse the save ("terrain zones were lost").** The check now allows the reset zones' own terrain objects, however many.
- **Saving a zone's ground with all its terrain objects deleted was refused.** Deleted terrain objects are now left out of those to patch, and the zone gets a new one.
- **Refusing a world in an older save format named the wrong format.** It now names the first chunk file whose format differs.
- **A corridor gate's band of wall could fall in a stave or wood dungeon.** It now runs on into the walls.
- **The save message could count the objects a zone reset cleared wrong, even below zero.** It now counts the objects the deletions removed.
- **An object changed in the inspector lost the high flag bits it was saved with.** The unused bits are now kept and written back.
- **A player whose pieces had all gone from the live game could still be named the world's top builder.** They are now taken out when their count reaches zero.
- **The ground around locations was grass where the game shows dirt, paving or cleared ground.** The paint of locations (and of rocks that leave dirt) is now applied with the game's falloff, so the ground's colour, Regrow's vegetation rules and the paint Uplift puts back around locations match the game.
- **After a ground disc was added or taken away, a zone could keep the ground of the old discs.** They are now swapped together.
- **A location rule of no biome, or of a biome flag the map does not know, kept the seed's locations from being laid out.** Such a try now finds no zone and the next one is made.
- **A heightmap picture imported into a selection sat about a metre off its outline, and its size was one point too wide on each side.** It is now fitted to the selection's own grid box.
- **A selection whose outline crossed itself showed the wrong area (a figure eight about 0 m²), and Place scattered too few or too many objects in it.** The area is now what the selection picks.
- **A box selection dragged along one axis was kept though it had no area, so actions changed nothing or edited a line at half strength.** A box is now dropped when it is less than a metre across either way.
- **Area Replace replaced nothing when the kind's name held " (" (a mod's).**
- **A terrain modifier that smooths with a radius of 0 (odd mod data) made an invalid height at its middle.** Such a modifier now smooths nothing.
- **Claude's set_control could set a slider or number to NaN, and failed on a number too big.** NaN and the infinities are refused now, and a number box's value is held to its range.
- **Claude's remove_objects and select_objects counted a repeated id as one that was not an object.**
- **Shields and banners Claude put in a chest always got the first style.** set_contents now passes the item's Variant on.
- **Claude's set_dungeon_door answered "Now pending" when no door was put in, and close_dungeon_ends could answer nothing.** The tools now say what the Dungeon tool said (or "Nothing changed.") when no step was made.
- **Claude's read-only looks changed your view.** support_check left the Workshop's Support switch on, and screenshot (one view or four) left the camera where it had looked; the switch is no longer touched and the camera is put back.
- **Claude's forest failed with a confusing script error for a name ending in a backslash.**
- **On slopes, pieces Claude placed on the ground floated or sank by up to half a metre against the height ground_at gave.** Placing, moving and replacing now take the ground between grid points, as ground_at does.
- **Claude tools given only one coordinate of a point went on without a word.** list_objects dropped its "near" filter, and move_objects and copy_objects turned round the given x and the objects' middle z; list_objects, move_objects, copy_objects, search_world, list_locations, screenshot and add_blueprint now refuse a lone coordinate.
- **Claude's replace_objects lost each object's size, and replacing a ship's root left its parts hanging on nothing.** The new object keeps the old scale, and a linked group's root takes its parts away with it, as the Area tool's Replace does.
- **Claude's build_roof gave a bare tool error, or ran on to a runaway piece count, when the roof piece had too few snap points or none sloping one way.** It now says why no roof was laid.
- **Claude's blueprint tools kept using an old Homestead folder, so a Homestead installed or moved since was not seen.** list_blueprints, open_workshop and add_blueprint now look for Homestead again.
- **Opening the Workshop failed when there were no pieces to build with (the game's data missing, or every piece hidden by the mods' manifests).** It now chooses none and says so.
- **Live, a dungeon laid out or gone in the game kept showing as it was until an unrelated edit.** It now updates when the game changes.
- **A formula mixing && and || worked out differently from the web editor (1 || 0 && 0 gave 0).** && now binds tighter than ||, as in JavaScript.
- **Formulas refused numbers written with a capital E (2E3), and sign() of a NaN failed at every point.** Exponents take e or E (also after a leading point, .5e1), and sign(NaN) is NaN, as in JavaScript.
- **Erode made or destroyed ground at soft edges and locked points, and drops sped up going uphill.** A drop now carries exactly what the ground gives or takes, leaves its load along its way (or back where it took it) when it stops, never digs below where it goes nor fills above where it is (so no runaway holes), and slows going uphill.
- **The Area tool never said when its ground actions (Erode among them) reached the game's ±8 m limit, as strokes, Shape and paths do.** The Area panel and Claude's shape_ground now say so.
- **The game's look banner said the editor works without the game's files, with plain colours, though no world, server or Workshop opens without them.** Both hints (Valheim not found, its files not read) now say worlds need them.
- **Claude's reset_zones with a huge rectangle could hang the editor or run out of memory.** The range is now cut to the area's zones first.
- **In the Generate panel, with the biome's style, choosing another biome kept the old biome's decay.** The decay now follows the biome too while Style is the biome's.
- **Delete in the Dungeon tool could remove the wrong room after an undo.** The selection now remembers the room and finds it again, and is cleared when the room is gone.
- **Copies pasted with a negative gap could land at another height than the preview showed.** Every copy's height is now read before any ground is shaped, as the preview reads it.
- **After Discard, steps undone in another area could still be redone.** Discard now drops those too, as a new edit does.
- **Removing a change from the history could unmark ground a later step had modified.** A modified flag a later step changed is now kept.
- **Copying an area with no objects reported success, and pasting it did nothing.** Ctrl+C and Ctrl+X now say there is nothing to copy and keep what the clipboard had.
- **A blueprint of ground more than 327.67 m above or below its middle lost height.** Heights are now written as they are rather than cut to the web editor's 16-bit range.
- **Opening an area could fill it with removed objects nothing referred to.** Only the things of steps kept in the area are added now.
- **A dungeon whose rooms failed to read in an unexpected way made the editor start the read again on every frame.** The failure is now logged and the rooms shown are kept until the area changes.
- **A brush stroke or drag could keep going after the 3D view lost the mouse.** When the release went elsewhere (a dialog, another window), the stroke kept painting at the hovered point until the next click; losing the pointer now ends it where it last was.
- **A late change from the previous area could break or clear the 3D view of the new one.** It caused wrong objects, an error, or a cleared selection; the view now stops following the last area when another is shown.
- **On a strict OpenGL ES driver the game look's water depth and foam could come out wrong.** Textures are now read at high precision.
- **After lowering the 3D resolution and setting it back to Sharp, the smaller picture kept its graphics memory, and one the graphics card could not use went unnoticed.** It is now freed when unused, and an incomplete one is logged and the view drawn at full resolution.
- **Objects scaled up (a rock at ten times its size) vanished from the 3D view far too close.** How far a kind of object is drawn now takes the largest scale any of them is drawn at.
- **Moving the area with its arrows or going to a player outside it could lose a drawn path, or switch areas during a live apply.** They now refuse and say why while work is unfinished, as resizing and Follow do.
- **After placing a mountain, why nothing grew on it was erased.** The reason growing could not be done (no world generator, area too large) now stays, and the undo hint names one step when nothing grew.
- **Applying an inspector change could end in an error dialog instead of a message.** When another area had been opened meanwhile or the object's world file could no longer be read, it now says so and changes nothing.
- **The inspector blamed the game for objects you had removed.** Applying a change to an object deleted, replaced or undone since it was opened always said it was removed in the game, even offline; offline it now says what happened, and live it names both.
- **In the inspector, removing a container's items value could silently do nothing, and a value could be added twice.** The items value cannot be removed while its contents are editable (empty them instead), and a key already there is refused.
- **The inspector could write NaN or infinite numbers into an object.** Such values, or numbers too large for the game, are now refused like any other value that does not fit.
- **The inspector's durability column said "%" and dropped fractions.** It is now labelled as points and keeps fractions, and a stack has the same maximum (65535) in the details rows and in the picked slot.
- **Taking back Claude's changes did not say that Ctrl+Z brings them back one at a time.** The message now says so when there were several.
- **A save could be reported as failed though the world was written, and a failed history write left a ".new" file behind.** Every failure there is now logged instead, and the half-written file is deleted.
- **Ground tools could fail to say a point hit its limit.** Under No limit the ±400 m lift was never reported, and a smoothed point stopped short by the level's own ±8 m went unnoticed; both are now reported.
- **Reloading a live world from the map could end in an error dialog with the map not refreshed.** It now says "Could not reach the game" and the busy veil always goes.
- **Placing a dungeon could end in an error dialog.** When the world's files could not be read, or a dungeon came with no rooms, it is now said in the status bar and nothing is placed.
- **Pasting a dungeon blueprint with nothing this world can make placed an empty entrance, and one holding only its portal got the wrong reason.** The check now counts as placing does, and a portal-only blueprint is said to hold only its portal.
- **Pressing / opened Help, though ? is the key it advertises.** Help now opens on ? (Shift) or F3.
- **A map close-up that failed to read was retried on every frame, with nothing logged.** It is now logged and not asked for again until the view, the edits or the map change.
- **Pieces deleted from the map could come back until the next refresh, and a failure to read buildings showed none silently.** Only the latest read is shown now, and a failure is logged.
- **The map's distance-from-the-centre filter silently ignored numbers it could not read.** "1,5" or "2 000" was taken as no limit and "NaN" made nothing match; spaces between thousands and the computer's decimal mark are now read, and anything else is said in the zone filter's line.
- **The new-world page showed distances like "000 m" and "1000 m".** Distances are now rounded to 100 m, shown as "< 100 m" when that close and in km from 1.0 km.
- **Typing an invalid seed on the new-world page left the last seed's dungeon search running.** The search now stops, the marks go and the dungeon list empties.
- **Leaving the new-world page during a seed search could end it with "The search failed".** This no longer happens.
- **A slope mask no longer lets steeper ground through on the area's border.** The slope there read about half; each direction is now divided by its own step.
- **Measure's lowest and highest heights now follow ground edits and include the two points.** They used to go stale after an edit and could miss a point's height; they now refresh after an edit and when Measure is chosen again.
- **A double click on a Workshop button no longer opens the Workshop twice.** A second open while one is under way is now refused with a message.
- **A blueprint opened from Homestead's folder by another spelling of its path is saved back without asking.** A trailing separator, a link to the folder, or another letter case on Windows used to make the editor treat it as a blueprint from elsewhere.
- **A click with a small Place brush now places an object.** A 2 m brush at the default density held none, giving an empty preview and a click that placed nothing while dragging did; the layout now holds at least one.
- **Stacked layers can no longer take a line past 2,000 pieces.** 20 layers on a long end-to-end line made up to 40,000, recalculated on every change; the cap now counts every placement.
- **A script that changes the ground from several threads (Parallel.For) no longer loses changes or fails.** Set, Paint, Mountain, Remove, Place, Move and Print are now safe to call from several threads.
- **A script setting the ground at a point that is not a number no longer edits the area's west or south edge.** Points that are not finite numbers are now outside the area.
- **A performance log that cannot be written no longer fails drawing every 2 s.** A refused write (a read-only data folder) is caught, the first is logged, and the lines are dropped.
- **A --data folder that cannot be made now stops the start with a message instead of a crash.** It is said like any other option that cannot be used, and the editor exits with code 2.
- **Saving a script under the name of another saved script now asks first, and one unreadable script no longer hides the others.** The file is also written safely, so a crash mid-write no longer loses the script; an unreadable script is left out of the list (and logged).
- **The script API's Objects.OfKind now lists every kind of object.** Furniture, Remains, Creatures, SpawnPoints, Nests, Dungeons and Effects could not be found by script authors or Claude.
- **A save that cannot be read back no longer leaves its changes waiting to be written again.** After zone resets or No limit ground, when reading the world again (or removing the earlier saves) failed, the next save refused the session's own save as one the game made. Removing earlier saves now only logs a failure, and a failed read starts the session over and says to open the world again.
- **Typing a place for a selection that holds a runestone now puts the other objects there.** The typed place was measured from the middle of everything selected, but runestones never move, so the objects landed off; it is now the moved objects' own.
- **The Noise brush's ragged edge is no longer cut straight where the noise dips lowest.** The Noise shape now looks 1.5 radii out instead of 1.42.
- **A light first touch of paint no longer clears the generated dirt around it.** Even a 1% soft edge used to snap to clear ground and leave a hard ring; brushes, Path, Area and pasted paint now start from the generated paint (dirt around locations, biome colours).
- **A live world that fails to open no longer leaves its connection open.**
- **Leaving a live world while Apply live is still under way no longer loses the apply's outcome.** It used to fail with an error instead of giving the outcome.
- **A Discard that cannot read the world no longer makes the next save refuse the session's own save.** The world is now read first.
- **Opening the saved-worlds page no longer freezes on a slow folder or stops half set on a broken one.** The first look for worlds now runs away from the window's thread, like later checks.
- **A crash while the loaded stamps are written can no longer lose them all.** stamps.json is now written whole through a temporary file, and a failure to write is logged.
- **Settings that cannot be saved are now said, not shown as saved.** The Settings window used to close as if saved when the settings file could not be written (a read-only data folder); it now shows why and keeps the settings as they were.
- **Saving from the 3D view worded the changes differently from the map.** Both prompts now describe the pending counts the same way (and translate correctly).
- **The tool rail mixed languages.** Natural, Cultivate, Clear and the other rail labels and captions stayed English, as did a tip on the start page; they now follow the chosen language.
- **A PlanBuild export lost chest contents and sign texts, and a name with a line break broke its header.** An exported .blueprint now opens again with what its objects hold (objects without a prefab are left out).
- **A power cut right after a save could leave an empty blueprint, preferences file or game cache.** These files are now written as durably as the settings.
- **A game mesh with a short vertex buffer could stop all of the game's data from being read.** A vertex past the data is now left at 0.
- **The Workshop's library showed a description differently from a paste of the same file.** The library trimmed every quote off it ("Big" house lost its first quote) and matched header names in any case; both now follow the same rule as a paste.
- **A crash right after making a new world could leave a save the game trusts with files missing.** The new world's files are now flushed to disk and the .ok marker is written last.
- **Searching blueprints found different ones in the Workshop's library and for Claude than in the Blueprints panel.** All three now search by every word in the name, creator, description or tags.
- **Esc did not close message boxes.** It now does.
- **A typed path starting with another user's ~name (`~bob/worlds`) became `<home>bob/worlds`.** The world folder box, the settings' folders and the SSH key path now expand only ~ alone or before a separator.
- **Claude's Workshop prompt said the user saves the blueprint, while Claude's instructions describe save_blueprint.** The build_in_workshop prompt now tells Claude to save with save_blueprint when you ask, and the server's summary says the Workshop's building is the one thing Claude can keep.
- **The start page's bridge address went back to 127.0.0.1:5182 at every start.** The box now starts from the address you last connected to.

### Changed
- **The terrain look no longer leaks memory** each time the terrain shader is rebuilt.
- **Dungeon dressing makes slightly different dungeons.** Several steps drew their random counts more than once; the same seed now makes a slightly different dungeon.
- **A dungeon's throne now faces the way it was placed.**
- **Dungeon wall windows are the same for a seed whatever the system's regional settings.**
- A copy at scale 1 no longer stores an explicit scale (and drops the template's scale), as new blank objects do.
- A failed Apply no longer leaves Claude's server behind when its port is taken.
- A shader that fails to compile or link no longer leaves graphics memory behind.
- **The Mask panel shows every setting of a mask changed elsewhere** (biomes, slopes, paint), so writing the boxes back can no longer undo that change.
- Location markers and the View panel's location count now share one 40 m test, which no longer reaches 1 m too far east and north.
- The Path tool looks for points to change as far out as its natural look's ragged edge can reach.
- A history step marked applied or removed can no longer be lost from the kept history.
- **Faster everyday editing.** Choosing a builder no longer reads every character file again (a file is read again only when changed); typing in the blueprint searches no longer reads every blueprint and decodes every picture again, and the start page lists blueprints and decodes pictures in the background. Dragging a Place share slider or ticking kinds saves place.json once after a short pause, and opening a world folder or Settings is lighter (settings written once, mods' extension files read in the background).
- **Faster saving and restoring.** Saving new objects, merging the game's live changes, looking up chunk files and saved zones, and re-reading the world after No limit ground or zone resets all do less repeated work. Restoring objects from a backup reads each chunk file once and only compares objects of the same prefab within a metre.
- **Less memory and less work in the 3D view and map.** Brush strokes send only the changed part of the ground to the graphics card, and strokes no longer re-look for locations, re-allocate or redraw when nothing changed. The map no longer rebuilds its zone outlines and colours every frame, a closed seed map no longer keeps a screen-sized close look, the world map's overview (about 58 MB) and backups read for the Area tool's restore are freed when no longer used, and window screenshots with a dialog open free the paint that dims the window.
- **Faster ground and dungeon work.** Crunched textures are checked once per load, cut-out textures' colour bleed looks only at the edge, building the ruins for a scene goes through objects once, and building stability, hammer snapping and the location layout work only on nearby things. Dungeon stairs, two-floor rooms and room dressing do less repeated searching, and Regrow, the seed preview, shape formulas, hydraulic erosion and hill and range mountains allocate less.
- **A faster start page and fewer waits.** The start page asks every installed bridge at once, so silent ports no longer add 1.5 s each, and looks for Homestead only when the game's state changes instead of every 2.5 s. Game logs for the location lists' order are read only when they changed, a game's catalogue report and the sorting of objects into kinds do less work, and the log writes text in one piece and no longer writes Windows line ends as lines of their own.
- **Smoother History, selection and overlays.** Taking a step out of the history or marking a zone's ground for reset redraws only the ground it changed, and going back or forward several steps refreshes the window and asks for a live apply once. Drawing a selection zone, the pending counts in the save bar, the live players' name labels, the Measure panel, and ward and crafting-station ranges for the overlays all do less work; the smooth brush and brush frames that change nothing (locked or masked ground) no longer redraw the ground.
- **Loading an area or growing it with Follow** no longer allocates and drops a copy of the area's empty ground (about 35 MB on 17x17), and editing with no mod extension that cuts the ground goes over the cut grid once per change.
- Claude's area map no longer holds up the window while it reads the paint and sorts the objects, and Claude's place_pieces looks only at objects near the pieces it puts down.
- Internal code cleanup; no change for users.

## v1.45.108 — 2026-10-17

### Changed
- **Saving new ground in many zones no longer reads the same chunk file again for each one.** The object is now read alone, once per save, instead of a whole chunk file (often several megabytes) per zone.

## v1.45.107 — 2026-10-17

### Changed
- **A long live session no longer keeps every past read of the game in memory.** Memory used to grow all session as moving creatures and burning fires changed at every check; it is now freed as objects are read again.

## v1.45.106 — 2026-10-17

### Changed
- **Generating a deep dungeon is faster.** Adding the loops between rooms did hundreds of millions of steps with many levels; the dungeons made are the same.

## v1.45.105 — 2026-10-17

### Changed
- **Apply live after a big clear no longer reads every removed object first.** Clearing 20,000 trees used to mean 20,000 reads for nothing, holding up the live view; an object's data is now read only when it may move.

## v1.45.104 — 2026-10-17

### Changed
- **Saving and Apply live no longer copy every edited zone to find the few they need.** With 2,000 edited zones, each save and each Apply live (with Auto apply, after every edit) used to copy some 250 MB even with nothing lifted. Only the zones needed are copied now, once; the map's edited-zone list, the zone statistics and the edited-zone marks drawn ten times a second copy none.

## v1.45.103 — 2026-10-17

### Changed
- **Following a live world no longer slows down after a big Apply live.** After a 3,000-piece paste, any change in those zones used to cost millions of comparisons at every check of the game; the objects the editor made are now found at once.

## v1.45.102 — 2026-10-17

### Changed
- **Opening an area takes about half as long to work out the generated ground.** The game's base paint is now kept from the first, parallel pass instead of working the ground out a second time.

## v1.45.101 — 2026-10-17

### Changed
- **Dragging an Area selection no longer stutters on a big area.** The outline still follows the mouse at once; the counts and lists are now worked out once the moves waiting are handled, and the cut and fill only for Flatten, Raise and Lower.

## v1.45.100 — 2026-10-17

### Changed
- **Regrowing nature in the Ashlands reads each zone once.** The Ashlands' wide groups used to make neighbouring zones be read again for each zone next to them (about three times the reading over a 4 × 4 block); the same spots are grown.

## v1.45.99 — 2026-10-17

### Changed
- **Apply live no longer waits for the history file to be written.** After each Apply live (with Auto apply, after every edit) the whole history was written to disk in the editor's window, longer as the history grew. It is now written in the background, one write after the other, with the world as it was at the apply (what players change meanwhile is merged once the objects are read); opening the world again or closing the editor waits for the writes still to do.

## v1.45.98 — 2026-10-17

### Changed
- **Regrowing nature over a big area no longer stalls the editor.** A Mountain with Grow over 49 zones used to stall for seconds checking spots against every object of the area; only nearby objects are checked now, in the background. The same spots are grown (Area Regrow, Mountain Grow and Claude's regrow_nature).

## v1.45.97 — 2026-10-17

### Changed
- **Picking a big saved selection no longer freezes the editor.** 5,000 objects in a 300,000-object area used to make about 1.5 billion comparisons; the same objects are selected.

## v1.45.96 — 2026-10-17

### Changed
- **Long paths apply without freezing the editor.** A 1.5 km diagonal path in a 17 × 17 area used to freeze it for seconds; each metre of the line now looks only at nearby points, with the same result. A Cave also looks only near each tree and rock instead of checking every one against every metre of the line.

## v1.45.95 — 2026-10-17

### Changed
- **Editing with the map zoomed in no longer piles up memory.** The map used to keep up to seven close-ups (about 400 MB in all) for pictures never shown again; it now keeps the last two close-ups of the current edits only.

## v1.45.94 — 2026-10-17

### Changed
- **The map stays light with many search results.** With 2,000 results the map used to make about 100,000 arrays a frame even when idle; pin shapes are now made only when the results, the chosen one, the zoom or the players change.

## v1.45.93 — 2026-10-17

### Changed
- **Switching between the map and the 3D editor no longer stalls the map's first frame.** The game's minimap textures are now decoded once (again only when another Valheim folder is chosen) instead of each time the map came back.

## v1.45.92 — 2026-10-17

### Changed
- **Opening another area no longer reads the game's terrain look again.** The terrain shader and its textures (tens of MB) used to be read again for each area shown, with plain colours meanwhile; they are now read once, and again only when another Valheim folder is chosen.

## v1.45.91 — 2026-10-17

### Changed
- **Areas with many dungeon rooms or location parts draw faster.** They are now sent to the graphics card once, and only those in the parts of the area in view are drawn, like objects.

## v1.45.90 — 2026-10-17

### Changed
- **Showing spawn points or nests in a big area no longer costs every frame.** They are now found once and looked for again only when the objects change.

## v1.45.89 — 2026-10-17

### Changed
- **Moving the mouse in the Select tool over a big area no longer bogs the window down.** The object under the pointer is now looked for once for all the moves waiting.

## v1.45.88 — 2026-10-17

### Changed
- **Moving or dropping a big selection among many objects is much faster.** Both the drag and End (drop) now look only at what is near; what snaps and where things land are unchanged.

## v1.45.87 — 2026-10-17

### Changed
- **Finding where locations go (new worlds, seed search) is faster.** Lookups for corners west of the world's middle used to walk chains of hundreds of entries.

## v1.45.86 — 2026-10-17

### Fixed
- **Connecting to a server whose plugin did not answer could freeze the window for up to 15 seconds.** The check of the server's Valheim folder that says why now runs in the background, as does reading the plugin's port from the server's settings.

## v1.45.85 — 2026-10-17

### Fixed
- **Pasting a hand-edited or older blueprint could stop half-way and leave No limit on for every later brush.** A blueprint whose ground data did not match its size could stop the paste part-way, leaving the ground partly changed with no history step and the switch that lifts the game's ±8 m limit stuck on. Such blueprints (including a size that is not a grid or an unnamed object) are now refused with the reason, a blueprint without an outline is pasted by its size, and every tool that briefly lifts the limit (paste, clear the site, Mountain, scripts) switches it back off even when it fails.

## v1.45.84 — 2026-10-17

### Fixed
- **Saving or applying ground lifted past the ±8 m limit in two far-apart places could run out of memory.** Two lifts at opposite ends of the world were worked out as one huge grid. Far-apart lifts are now handled separately, each on its own few zones.

## v1.45.83 — 2026-10-17

### Fixed
- **The Place tool could lose its presets, favourites and weights, or fail to open.** A write cut short could leave the Place settings file broken, and the next save overwrote it, while a hand-edited file with empty values crashed the Place panel or loading a preset. The file is now written safely, a broken one is first copied aside as place.json.bad, an unreadable one is never overwritten, empty values are treated as empty, and presets without a name or kinds are left out.

## v1.45.82 — 2026-10-17

### Fixed
- **A crash while saving selections could lose every world's saved selections.** A write cut short could leave the saved-selections file broken, and the next Keep or Forget overwrote it. It is now written safely, a broken file is first copied aside as selections.json.bad, and one that cannot be read (or kept aside) is never overwritten: Keep and Forget tell you they could not.

## v1.45.81 — 2026-10-17

### Fixed
- **A script could put objects at impossible places into the world.** `Objects.Place` accepted positions, turns or tilts that were not numbers, negative sizes and points outside the open area, and `Objects.Move` accepted infinite values and places outside the area (a piece snapped near the edge could also end past it). These now stop the script with an error saying what was wrong (NaN still means "on the ground" for `Place`'s `y` and "as it was" for `Move`), and the boulder and ring-of-trees examples keep their objects inside the area.

## v1.45.80 — 2026-10-17

### Fixed
- **Rooms of a generated dungeon with alcoves had their furnishings off to one side.** An alcove shifted the room's middle and width, so a hall's colonnades, braziers and other paired pieces stood off-centre, with one of each pair in the wall or missing, and a mirrored side room was 2 m off. Furnishings now follow the room's own walls, and an alcove is extra floor beyond them.

## v1.45.79 — 2026-10-17

### Fixed
- **The warning sign by a generated dungeon's locked boss gate was sunk in the wall.** It sat about half a metre deep in the stone on the arena's side. It now hangs on the antechamber's side, on the wall's inner face.

## v1.45.78 — 2026-10-17

### Fixed
- **The steps before the plinth of a generated burial chamber, chapel, shrine or tomb rose away from it.** They faced the plinth and climbed toward the room, unlike the great hall, arena and feast hall. They are now turned like the others.

## v1.45.77 — 2026-10-17

### Fixed
- **A generated cell block's elite guards could stand locked inside the cells.** Elites and their spawners could be placed anywhere in the room, behind the grates. Both rows of cells are now kept clear, up to their grates.

## v1.45.76 — 2026-10-17

### Fixed
- **A generated dungeon could hold two kitchens, two forges or two feast halls.** A mirrored side room could duplicate a room kind that should appear only once in a fortress, prison or Dvergr hold. Such rooms are no longer mirrored.

## v1.45.75 — 2026-10-17

### Fixed
- **A generated dungeon could hold extra antechambers that led nowhere.** When the boss arena did not fit, the antechamber stayed, so a dungeon could have two or three dead-end "unique" antechambers with elites and no gate, or one with no arena at all. The antechamber and its corridor are now taken back whenever the arena does not fit beyond it.

## v1.45.74 — 2026-10-17

### Fixed
- **A generated dungeon's hidden cache could sit in the upper half of a tall room on the level below.** A cache over a tall room (great hall, chapel, throne room, arena) floored its upper 4 m. No room or corridor is placed over a tall room any more.

## v1.45.73 — 2026-10-17

### Fixed
- **A kind of object whose model failed to load was never drawn at all, and no other model got its textures.** If a model failed outright (for example, memory ran out on a texture), its objects were neither drawn nor pickable, their ghosts never showed, and later models using the same textures came up without them. The failure is still reported, the objects now get the stand-in box, and the textures are released for other models.

## v1.45.72 — 2026-10-17

### Fixed
- **In the Workshop, a wall crossing the cut height was picked and hovered instead of what stands inside.** A wall's hidden top took the click meant for the chest behind it. Picking now only meets an object's shape below the cut, as drawn.

## v1.45.71 — 2026-10-17

### Fixed
- **Pictures of the 3D view and the map came out part-transparent under water, glass, ghosts and overlays.** `--shot`, the docs' pictures and Claude's screenshots were see-through there, and the view could look washed out where the window shows what is behind it. Blending now keeps the picture opaque, and the saved PNG is always opaque.

## v1.45.70 — 2026-10-17

### Fixed
- **Typing in a text box moved the camera or changed how it moves, and so did keys on the start page and the map.** An F typed in a search box switched the camera between orbiting, walking and flying, W, A, S or D moved it, and Space on the start page was taken from the focused button. The 3D view now takes a key only when the 3D editor is shown and nothing is being typed in a box.

## v1.45.69 — 2026-10-17

### Fixed
- **A chest filled through the editor could hold a stack larger than the game allows.** The inspector's Contents and Claude's `fill_object` accepted any count up to 65535, and the game lost the excess. Each item's largest stack is now read from the game's files (so the cached catalogue is read again), and a larger stack is cut to it with a note such as "Stone: stack of 70000 cut to 50 (the item's maximum)".

## v1.45.68 — 2026-10-17

### Fixed
- **Claude could run scripts on your computer without your say.** `run_script` ran C# with your rights (your files included). Scripts from Claude now need your permission under **Settings → Claude → Claude may run scripts** (off by default): while it is off, `run_script` (dry runs too) and the Script tool's Run through `set_control` are refused with a message saying where to allow them, and Claude's other tools and your own scripts work as before. The separate process scripts run in (v1.45.67) protects the editor from crashes but is not a sandbox: a script still has your rights.

## v1.45.67 — 2026-10-17

### Fixed
- **Script: Stop could not end a script busy in one long call.** Stop and the 2-minute limit only worked at the next loop turn, so a single long call (a huge query, `Thread.Sleep`, endless recursion) ran until the editor closed, burning a core. Scripts now run in a process of their own: Stop asks it to end at the next loop turn and kills it a second later if that was not enough, and a script process that crashes is reported as an error. This keeps a crash away from the editor but is not a sandbox: a script still runs with your rights.

## v1.45.66 — 2026-10-17

### Fixed
- **Claude: a step made through `set_control` was not named "Claude: …".** A control Claude pressed (a button, a toggle, a tool's Apply) labelled its step as yours, so the history and **Take back Claude's changes** could not tell it from your own work. Steps made while Claude sets a control now get the "Claude: " prefix like all of Claude's other steps.

## v1.45.65 — 2026-10-17

### Fixed
- **New world: a name the game accepts on one system broke the world on another.** On Linux and macOS "My:World?" went through (and the world could not be copied to Windows), and on Windows "CON" or "COM1" went through and failed when files were written. The check is now the same everywhere: no / \ : * ? " < > |, no device name, nothing ending with a dot or a space.

## v1.45.64 — 2026-10-17

### Fixed
- **A blueprint file with terrain marks far apart, or with numbers that are not numbers, froze or failed the import.** Pieces two kilometres apart meant millions of ground cells (the editor froze or ran out of memory), a huge radius overflowed, and "NaN" was taken as a position. A line with a non-finite number is now skipped like an unreadable one, the ground grid reaches only as far as the terrain marks, and marks spanning more than 1200 m are refused with the reason, on import and on paste.

## v1.45.63 — 2026-10-17

### Fixed
- **A settings file held by another program for a moment was written over with the defaults.** If the settings could not be read at start (a sync or antivirus program holding the file, or no permission), the next saved change overwrote them, losing the game folder, extra folders, allowed extensions and Claude's token. A failed read is now retried, and if it still fails the defaults are used for the session but never saved over the file, which the log says.

## v1.45.62 — 2026-10-17

### Fixed
- **One unusual bundle (a mod's, damaged, or laid out otherwise) left every mod model undrawn.** A bundle without an assets file, or whose table of contents could not be read, ended the whole start-up index, so no mod model was drawn. Such a bundle is now skipped and noted in the log, and the rest are indexed.

## v1.45.61 — 2026-10-17

### Fixed
- **The editor could fail to start, or miss pieces and models, depending on where the game's scripts are in its bundles.** A bundle that was already open could get closed when read a second time, so the next lookup in it failed (stopping the start) or found nothing. An already-open bundle now stays open until its last reader is done, which also stops a file handle from leaking.

## v1.45.60 — 2026-10-17

### Fixed
- **On Windows, a mod updated while the editor ran left its models missing until the editor was restarted.** The old copies of the mod's bundles were still held open, which Windows refuses to delete, so the copy failed and no mod model was drawn for the rest of the session. The old files are now released before the copies are replaced.

## v1.45.59 — 2026-10-17

### Fixed
- **One damaged object ended a world search, and a damaged item stand broke the inspector.** Search world, the inspector's chest and stand reads and Claude's get_object only skipped an object with short data; other damage (bad counts or lengths, as in damaged saves) ended the whole search or the inspector. Every read now skips what cannot be read, however it is damaged, and goes on with the rest.

## v1.45.58 — 2026-10-17

### Fixed
- **A chest could be given something that is not an item, which the game then threw away.** The inspector and Claude's set_contents accepted any game prefab, so "Troll" or "piece_chest" went into a chest and was saved, and the game dropped it on load. Only the game's items are accepted now; anything else is refused with its name.

## v1.45.57 — 2026-10-17

### Fixed
- **Exporting To Homestead, importing a blueprint or generating a dungeon with a long name could hang the editor.** For a name that long, every "name (2)", "name (3)"… gave the same file name, so the search for a free name never ended. The name is now cut so that its number stays in the file name.

## v1.45.56 — 2026-10-17

### Fixed
- **Claude's set_control "click" on a switch, toggle or radio button changed nothing.** The click skipped the toggle itself (and a button's command), so the switch kept its value while Claude was told it worked. A click now presses the control like a real pointer: the toggle changes, then its handlers and command run.

## v1.45.55 — 2026-10-17

### Fixed
- **Claude Desktop lost the editor's MCP server on a batch request or a tool that ran too long.** The `--mcp-stdio` relay ended on a line of JSON that is not a single message (a batch, a bare value), on a method that is not a string, and when the editor gave no answer within 5 minutes, so Claude Desktop showed the server disconnected. Such cases are now answered with an error (including a timed-out request) and the relay keeps going until its input ends.

## v1.45.54 — 2026-10-17

### Fixed
- **Claude's object ids could hit the wrong objects after another area was opened or the world read again.** Ids were reused once another area was open, the same one reopened, or the world re-read after a save, so remove_objects, move_objects, set_object_data and others changed whatever objects now had those numbers. The tools now remember which area the ids were given for and refuse ids from another area, or from before the world was re-read, until objects are listed again.

## v1.45.53 — 2026-10-17

### Fixed
- **Claude could not open or add a blueprint whose file was not named after it.** open_workshop and add_blueprint looked for a file named after the blueprint's name from list_blueprints, so a numbered copy, a downloaded blueprint or one saved under another file name was "not in the library". The name is now looked up among the library's entries first, then by file name.

## v1.45.52 — 2026-10-17

### Fixed
- **A dungeon Claude generated was not one of Claude's changes, and left a file in the blueprint Library.** Its step was named "Dungeon: generated …" (in a world) or "Added …" (on the Workshop's plot), so Take back Claude's changes kept it, and on the plot its blueprint was written into your Library, which Claude is not to write to. The step is now "Claude: generated dungeon …", and on the plot the dungeon is added directly with no file written.

## v1.45.51 — 2026-10-17

### Fixed
- **A blueprint Claude's add_blueprint refused, because a piece would leave the area, took the place of what you could redo.** The refused blueprint was put down and undone at once, which cleared the redo list (and could drop the oldest undo step) and left it there for the next Ctrl+Y. What a blueprint would add is now checked first, and nothing is put down when it is refused.

## v1.45.50 — 2026-10-17

### Fixed
- **A piece dragged far did not snap to the building where it was dropped.** A piece dragged more than about 60 m to another building never snapped to it. Snap points are now gathered again once the selection has moved more than 30 m.

## v1.45.49 — 2026-10-17

### Fixed
- **"Move there" moved the selection a little on every axis, even the ones left alone.** The X, Y, Z and Turn boxes show rounded values, and Move there used all four, so changing only Y (or nothing) replaced every selected object by a copy shifted by up to 5 mm and turned by up to 0.05°, with a new undo step, new ids, and a destroy and create on a live server. Move there now moves only along the axes whose box was changed, by the difference from the exact place, and does nothing when nothing changed.

## v1.45.48 — 2026-10-17

### Fixed
- **Replacing a ship or another linked group's root left its parts behind.** Replace (the Area tool's, and the Select tool's "Replace the selection") removed only the objects given, so replacing a vehicle's root left its parts in the save with nothing to hang on. Replace now takes a root's parts away with it, in the same undo step, and says how many went; a part replaced on its own still goes alone.

## v1.45.47 — 2026-10-17

### Fixed
- **A heightmap read but not yet put could land in another area.** The picture read by Import was fitted to the selection and area open at the time, but survived a change of selection or another area, so "Put it into the ground" wrote it into the wrong points. The picture is now dropped when the selection changes, and refused with a message when another area is open.

## v1.45.46 — 2026-10-17

### Fixed
- **Replace's "with" list kept the first world's kinds after another world was opened.** After opening another world, the Area tool still offered the first world's modded kinds, which the other world cannot copy, and lacked its own. The list is now rebuilt for another world or when the game's data or the mods change, keeping the chosen kind when the new world offers it too.

## v1.45.45 — 2026-10-17

### Fixed
- **Reset zones could say "Select an area first" or miss zones, depending on the Soft slider.** With Soft at 20 m and a 15 m wide selection, Reset found no zone, and a zone covered only by the outer half of the soft edge was left out. The zones are now those with a grid point inside the outline, whatever Soft is.

## v1.45.44 — 2026-10-17

### Fixed
- **Removing a move from the history could leave two copies of the object.** Removing a move (Remove, or "Take back Claude's changes") after a later step had moved the object again brought the original back while the later copy stayed, and removing a Place whose object was moved since left the moved copy. A change whose added objects were later moved or deleted is now refused with a message ("undo back to it instead"), and "Take back Claude's changes" says how many were left for that reason.

## v1.45.43 — 2026-10-17

### Fixed
- **A hand-made language file could stop the editor from starting.** A file in the data folder's `lang` folder that is not a single object (`[]`) or repeats a key crashed start-up, even for a language never chosen. A repeated key now takes the last text, and a file that is not one object of texts is logged and skipped like one that is not JSON.

## v1.45.42 — 2026-10-17

### Fixed
- **A translation of "Free" or "Cost unknown" with a brace in it broke the Build panel.** The cost line formatted these texts even though they take no values, which crashed the Build panel, the blueprint list and Claude's piece search. A text with no values is now shown as it is.

## v1.45.41 — 2026-10-17

### Fixed
- **Mirrored pastes tilted pieces the wrong way.** Pasting with Mirror on reversed the pitch of every object, so a beam sloping nose-down came out nose-up and tilted trees leaned the other way. Mirroring east to west now keeps the pitch, including for mirrored dungeon pastes and the paste ghost.

## v1.45.40 — 2026-10-17

### Fixed
- **A mod extension offered by two mods ran everything twice.** When two mods ship the same extension and both are allowed, its shapes and ground cuts were drawn twice, its inspector text and pasted-copy rewrites ran twice, and a failed hook turned off only one of the two in Settings. Each extension now runs once, and a failure shows on every mod offering it.

## v1.45.39 — 2026-10-17

### Fixed
- **The start page could say "Reading the game's files" for ever, or "ready" when it was not.** When one kind of the game's data could not be taken (after a game update changed it), the start page stayed on its progress bar, data was mixed between new and old, and no failure was logged; if the files were already read, a failure still showed "ready". The editor now logs what failed, retries the missing data next time, and the start page says some of the game's data could not be read; "ready" is shown only once the game data is in use.

## v1.45.38 — 2026-10-17

### Fixed
- **A cache that could not be written stopped every world from opening.** When the editor failed to save what it reads from the game (a full disk, a read-only data folder, two editors writing at once), the data was thrown away too: no world opened, the log said the game could not be read, and each try re-read the whole game. The New world page lost its bosses and traders the same way. A cache that cannot be written is now only logged, and the data read is used.

## v1.45.37 — 2026-10-17

### Fixed
- **A world map that failed to build said "Building the world map…" for ever.** If building failed (a damaged world file, for example), the map stayed blank with its status unchanged and nothing in the log. The map now says it could not be built and why, and the log has the details.

## v1.45.36 — 2026-10-17

### Fixed
- **A live world's map could show another world's players.** The map asked the game for players every 5 seconds without waiting for the last answer, so a hung game piled up questions, and a late answer after switching worlds put the old world's players on the new map. The map now asks a world's game again only once it has answered, and drops an answer for a world no longer shown.

## v1.45.35 — 2026-10-17

### Fixed
- **The map could pin the results of an older search.** A search started while another was running could finish first and then be replaced by the older one's results, pinning the wrong hits and opening them the wrong way. Results from a search still running when another world was shown, or when the world was read again (Discard, Reload) or saved, were also pinned wrongly. Only the latest search on the world as shown is pinned now, and one dropped because the world changed tells you to search again.

## v1.45.34 — 2026-10-17

### Fixed
- **Claude's screenshots could show the wrong view.** When Claude asked for several screenshots at once, another call could move the camera in between, so pictures came from the wrong camera and four views could be labelled with the wrong sides. Tools that set the camera or take a picture now take turns.

## v1.45.33 — 2026-10-17

### Fixed
- **A dungeon Claude generated could land in the wrong place, or go live.** Claude's generate_dungeon checked that an area was open and Auto was off only before generating, so opening another area or the Workshop meanwhile put the dungeon there, closing the area crashed the tool, and turning Auto on sent the dungeon straight to the game. It is now checked again before placing: nothing is placed and Claude is told why.
- **A dungeon added to the Workshop's plot could land on another plot.** Generate dungeon (from the Dungeon panel or Claude) could add the dungeon to a plot opened meanwhile. It is now added only to the plot it was made for; otherwise nothing is placed and the status bar says so (the blueprint stays in the Library).

## v1.45.32 — 2026-10-17

### Fixed
- **Errors on the map went unseen.** When opening an area failed, or a Discard or Reload from the map failed, the reason showed only in the 3D editor's status bar, which the map does not show, so the "Loading…" veil flashed and the message turned up later out of context. The advice after making a new world did the same. On the map these now also show in a dialog (not for a world left meanwhile), and the window's message dialogs are titled "Valheim World Editor", as the map's are, instead of "Save".

## v1.45.31 — 2026-10-17

### Fixed
- **After a failed Discard or Reload in the 3D editor, every tool silently did nothing.** When reopening the area failed, no area was open, so tools, Save and Undo did nothing, and the status bar said "Discarded: the world was read again." over the error. The map now opens instead, and a dialog says the area could not be opened again and why (also when another area asked for meanwhile fails too); the old area's history is not kept after the world is read again.

## v1.45.30 — 2026-10-17

### Fixed
- **A change finished after going to the map could not be undone.** A script, a Claude change or Grow still running when you went to the map finished in the closed area, so the change went into the world but its undo step was lost. Going to the map (or opening another area) now closes the area, so such work changes nothing and says so (a script: "The area was closed while the script ran"). An area that fails to open leaves the one shown open as before, unless the map or another area was asked for (or the world left) while it loaded, and an area that finishes loading after that is not shown over them.

## v1.45.29 — 2026-10-17

### Fixed
- **Trees grown on a mountain could float, be buried or be lost.** If you undid, drew another stroke or let Follow grow the area while a mountain with Grow on was still working out its trees and rocks, they could land on ground that was gone or in the old area, even though the message said they grew. Now nothing grows if the area changed meanwhile, and the message says why.

## v1.45.28 — 2026-10-17

### Fixed
- **Keys still worked while a world was being saved from the 3D editor.** Ctrl+S could start a second save, and Ctrl+Z, Follow or the save bar froze the editor until the save ended. Saving now shows the same "Saving…" veil as the map, and the editor's keys and Follow wait until it is done.

## v1.45.27 — 2026-10-17

### Fixed
- **Pressing Enter again during an Area action could do it twice.** Reset zones, Regrow nature and Restore from a backup could be started a second time while they were still working, adding regrown trees or restored objects twice, or changing the old area if another one was opened. Apply is now greyed out while an action runs, and an action whose area changed meanwhile applies nothing and says so.

## v1.45.26 — 2026-10-17

### Fixed
- **Choosing an SSH key in a format the editor cannot read could crash it.** An old DSA key or a damaged or cut-short key file now gives "The key file could not be read" instead of an unexpected error, and such a key in your .ssh folder is skipped. Key files are also closed once connecting is over.

## v1.45.25 — 2026-10-17

### Fixed
- **Clicking Connect twice could open two connections to the server.** This could leave a second SSH tunnel open unseen until the editor closed, or let a failed attempt close the other one's tunnel. A click while connecting now does nothing, and quitting or going back to Home mid-connection closes the tunnel as soon as it connects.

## v1.45.24 — 2026-10-17

### Fixed
- **The dungeon list could fail on one damaged dungeon, and gave wrong room counts in older worlds.** Each dungeon is now read in its own world version, so room counts are right in worlds saved before chunk files. A dungeon that cannot be read is listed with no rooms, and the log says which one and why, instead of the whole list failing.

## v1.45.23 — 2026-10-17

### Fixed
- **One damaged ground record made the whole world fail to open.** A zone's damaged or cut-short ground edits stopped the world from opening, although the game only loses that zone's ground edits. The world now opens, the damaged object is kept as it is, and a line in the log says where it is; ground data claiming more points than a zone can have is handled the same way.

## v1.45.22 — 2026-10-17

### Fixed
- **Live, an object the game listed twice could be counted twice or leave a ghost copy behind.** This could double an object in the editor's lists and raise its builder's piece count by two, or leave an entry that never went away when the object was removed in the game. Each object is now taken once, with its last data.

## v1.45.21 — 2026-10-17

### Fixed
- **Saving could stop with an error instead of saying why it could not save.** When a save could not copy the terrain object it needs for new ground (or the objects new ones are made from), you got an unexpected error. It now reports a save that did not happen, with the reason and nothing written, and new ground copies the first terrain object it can use.

## v1.45.20 — 2026-10-17

### Fixed
- **A power cut just after saving could leave a broken save that the game trusts.** The world's data and metadata copies (.db2 and .fwl2) are now flushed to disk before the save is marked complete, so a power cut cannot leave an empty or cut-short copy with the previous save already gone. On Linux and macOS the save folder is flushed too, before the marker and again before the previous save is removed.

## v1.45.19 — 2026-10-17

### Fixed
- **An Apply live that failed partway said nothing had been applied.** When a later part failed after the ground had gone to the game, Discard would undo steps in the editor that the game kept. The editor now says what went in and what failed and marks the steps the game has as applied; Apply live again sends the rest.

## v1.45.18 — 2026-10-17

### Fixed
- **A save or Apply live that failed still turned No limit ground into ground discs and cleared the undo history.** When nothing was written or sent (a full disk, or the game refused the ground), the No limit ground is now put back as it was, and the area and its history stay. When the game may have taken the ground, the discs stay and the area is read again, as before.

## v1.45.17 — 2026-10-17

### Fixed
- **Objects you had just applied live could vanish from the editor as if players had removed them.** The editor dropped them while they stayed in the game, and undo could no longer reach them. A read of the game that overlaps an Apply live is now dropped and the zone is read again.

## v1.45.16 — 2026-10-17

### Fixed
- **A dungeon placed on a live world could be built from the wrong object's data.** On a live world, placing a dungeon could give it another object's data, or fail, for models read since you followed the game. Each model is now copied from where its data is, as saving, Apply live, search and the inspector already did.

## v1.45.15 — 2026-10-17

### Fixed
- **Other users of the computer could read Claude's token.** On Linux and macOS, other users could read the token saved when **Allow Claude to connect** is on and use the editor's Claude tools. The settings file is now readable only by you, as the saved servers file already was.

## v1.45.14 — 2026-10-17

### Fixed
- **In another language, Claude could use the Blueprints buttons that write or delete files.** The Blueprints rows' buttons (**Details**, **.vbuild**, **To Homestead**, **Delete**) are now blocked for Claude whatever language the editor is in, and so is the Select tool's **Delete**. Claude's ids for controls without a tip key no longer change with the language or drop letters outside a–z.

## v1.45.13 — 2026-10-17

### Fixed
- **Editing a saved server could send another server's password and token to it.** **edit** now fills the whole form from the saved server: its own saved password and token (empty when not saved), no passphrase or port, and **Save password** ticked when its password was saved. Before, a password typed for another server was sent on Connect, and a saved password could be dropped from the file. The password is sent as typed or saved, spaces at its ends included.

## v1.45.12 — 2026-10-17

### Changed
- **The Area tool's object actions no longer stall on a big area with the Mask on.** Remove, Select, Restore from backup and Regrow nature on a large area with thousands of objects were very slow and held up the scene. They are now fast, and what is picked is unchanged.

## v1.45.11 — 2026-10-17

### Fixed
- **Restore from backup with the Mask on doubled objects.** An unchanged tree or rock where the Mask lets nothing through was brought back on top of itself. Objects where the Mask lets nothing through are now left as they are.

## v1.45.10 — 2026-10-17

### Fixed
- **The map's zone filter could judge one world's zones on another world's counts.** If you opened another world meanwhile, **Show matching** and **Mark for reset…** could use the first world's counts, and a **Mark for reset…** question answered after switching marked the zones in the world left behind. Counts read before the world was shown again are now dropped, and so is a marking whose question was open when the world was shown again.

## v1.45.9 — 2026-10-17

### Fixed
- **A brush stroke made while Apply live was answering could lose ground or undo wrongly.** With **Auto** on, a new stroke begun before the game answered could lose its ground so far, or have its undo write old ground back when the apply also read the area again (zone resets, No limit ground). The ground now takes the applied edits when the stroke ends, and a stroke under an apply that reads the area again is dropped, with a message to draw it again.

## v1.45.8 — 2026-10-17

### Fixed
- **Some script calls never ended, even with Stop.** `Area.Points(0)`, a negative step, a step too small to move, and `Ground.Shape` or `Ground.Mountain` with a radius beyond about 16,000 km could run for ever. Such a step is now an error, shapes only go through the part within the area, and Stop works at every point of `Area.Points()`.

## v1.45.7 — 2026-10-17

### Fixed
- **A script with an endless loop of its own could not be stopped.** A `while (true)` or long `for` loop of plain arithmetic, `Ground.Set` and `Ground.Height` ignored **Stop** and the 2-minute limit, kept a core busy, and made every later **Run** and Claude's `run_script` answer "A script is already running" until you restarted. Every loop in a script now checks for Stop, and error line numbers are unchanged.

## v1.45.6 — 2026-10-17

### Fixed
- **Claude's build_floor, build_walls and build_roof could freeze the editor.** A huge rectangle could freeze the editor for minutes or never finish. The rectangle must now be within the open area, and a layout of more than 5000 pieces is refused up front with a message to build it in parts.

## v1.45.5 — 2026-10-17

### Fixed
- **Moving objects past the area's edge sank them about 1000 m, or threw others up.** With **Put on ground** on, the Select tool's move (dragging, **Move there**, **Move by**) treated ground beyond the open area as 1000 m below sea level, so objects sank or the rest of the selection was lifted, and the save wrote those heights. Past the edge an object now keeps its own height.

## v1.45.4 — 2026-10-17

### Fixed
- **Claude could change a live world with Auto on through the editor's controls.** Pressing a tool's **Apply**, an Area action or a script's **Run** through Claude's `set_control` changed the world and Auto sent it to the game at once. While the world is live with Auto on, `set_control` now only sets the View panel's controls, and `list_controls` says why the others are blocked.

## v1.45.3 — 2026-10-17

### Fixed
- **Applying live again after a lost answer could make every new object twice.** If the game placed the objects but its answer never came back (dropped SSH tunnel, reset connection, an apply running past the 3-minute wait), the next **Apply live** sent them again and doubled them. The editor now re-sends such a call first under the same id, and the plugin answers with what it did instead of doing it again. This needs the plugin from this version; with an older one the editor sends as before.

### WorldEditorBridge
- `/objects` takes an `X-Call-Id` header: a call already done under that id (one of the last 16) is answered again with what was done, not done twice. `/status` lists `"once"` among its features, and editors only send ids to a plugin that lists it. `/status` also gives `"run"`, a new id each time the game starts: a call whose answer was lost is not re-sent to a game started since, and the apply fails until the world is reloaded.

## v1.45.2 — 2026-10-17

### Fixed
- **Live zone resets were sent again after the world failed to load back.** When reading the world back after **Apply live** reset zones failed (timeout, dropped tunnel), the resets stayed pending and the next apply reset the same zones again, wiping what players built since. Resets are now cleared as soon as the game has done them, and a failed read-back is reported as applied, without reading the world again.

## v1.45.1 — 2026-10-17

### Fixed
- **Undoing a second Reset zones on the same zones kept the new options.** A zone marked again with other options (say, without **Keep buildings**) stayed marked with the new options after Ctrl+Z, a redo or removing the step in History, so the next save could wipe buildings the first marking kept. Undo, redo and removal now restore each side's options, and the history file keeps both.

## v1.45.0 — 2026-10-17

### Added
- **The editor can be translated.** Its texts now live in language catalogues (`Core/Lang/en.json`, English); a catalogue dropped in the `lang` folder of the data folder (`fr.json`, `pt-BR.json`…) translates the editor, with English showing where a text is missing. **Settings › Language** chooses the language, by default the computer's when there is a catalogue for it. So far every hover tip (except the Inspector's) and Settings come from the catalogue, and the other texts will follow. How to translate: [docs/translating.md](docs/translating.md).

## v1.44.0 — 2026-10-15

### Added
- **A Mac build (experimental), for Apple silicon** (macOS 12 or later): `ValheimWorldEditor-<version>-macos-arm64-experimental.zip` on the releases page, with ValheimWorldEditor.app signed ad hoc (macOS asks once: **Open Anyway** in Privacy & Security; its README says how). It has not been tried on a Mac yet. The editor finds Valheim inside `valheim.app` in Steam's libraries (`~/Library/Application Support/Steam`), worlds in `~/Library/Application Support/IronGate/Valheim`, the game's log in `~/Library/Logs/IronGate/Valheim` and r2modman's profiles; **Cmd** works as **Ctrl** for clicks in the 3D view and Script's Ctrl+Enter, as it already did for shortcuts. Intel Macs are not supported yet.

## v1.43.0 — 2026-10-14

### Changed
- **Generated dungeons read more like buildings.** Side rooms grow in wings of their kin (a kitchen by the feast hall, cells by the guard room, an ossuary by the burial chamber, a forge by the workshop), a room's purpose sets its light (grand rooms brightest, the dead's and the cells dimmest), and each level down holds a fifth more of the top level's loot and foes. A corridor's niche now and then hides a foe, and dvergr open doorways carry the dvergr arch.

## v1.42.0 — 2026-10-14

### Changed
- **Generated dungeons vary far more.** Rooms used to be boxes entered in the middle of a wall, each kind furnished one way, with torches along every wall. Now:
  - **Room shapes**: crossings, shrines, tombs and arenas with notched corners (an octagon's feel), chapels, halls and throne rooms ending in an apse, and store rooms, quarters and barracks with a corner cut away or a neck at the door. Walls, beams, the plan's map and the furnishing follow the shape, and twins mirror it.
  - **Bent corridors**: a corridor may run ahead, turn, and enter its room from the side (often in catacombs, warrens, holds and ruins; seldom in a temple). Some rooms are entered on their long side.
  - **Two to four furnishings per kind of room**, picked by the seed: a mead hall, a hall of the dead, a bare hall of fallen pillars; a chapel's raised altar in a ring of braziers or a nave of rugs and candles; a tomb on a dais; bedrolls round a fire; stock stacked in a corner with a cart; shelves in aisles; a scriptorium; a war room; the boss enthroned over heaps of skulls; a drill room; a dolmen shrine; and more. New props: cobwebs in the corners, chains and hooks from the roofs, bedrolls, weapon stands, training dummies, an archery target, dolmens, candles (where monsters do not come back), a cart, a bath, a round table.
  - **Lighting styles** per room: standing torches, wall sconces, or braziers in the corners; corridors likewise.
  - **Floors**: in a stone or grausten dungeon, grand rooms get black marble tiles and lived-in rooms wooden boards, half the time (Furniture at Furnished or Rich). Stone pieces stand on the marble through them, as the game's support asks.
  - **Built up inside**: in stone and grausten dungeons a great hall, throne room or chapel may carry a loft over the way in (a deck on posts 4 m up, a rail, stairs up one side, braziers and a sentinel on it); arenas and shrines may be sunk a metre below ledges round the walls; a burial chamber may hold a vault, an inner chamber with the sarcophagus inside. Alcoves off rooms and niches off corridors are furnished (a sarcophagus across, a statue between candles, a bed in its bay, a shelf and crates; a single piece in a niche).
  - **Clusters**: beside its main feature a room gets one or two groups of things that belong together in its corners: a camp, a meal, a scribe's desk, stores, an armoury, an altar, bones, a workbench.
  - **Hero pieces**: big rooms may get one piece that stands once in the whole dungeon: a dolmen, a fallen giant's statue in parts, a grave slab, a giant's skull or ribs, an altar of crystal, a cart.
  - **Corridor kinds**: a gate midway under a band of wall, a half collapse, bones along both walls, a runner, vines, sharpened stakes. **Dead ends**: corridors that lead nowhere but a cache (the biome's chest, loot, sometimes a guard). Catacombs and prisons keep some corridors dark between lit rooms.
  - **Pacing**: a few rooms stand nearly bare (one piece, a light); clutter never repeats the same thing twice running, and rooms differ in how tidy they are.
  - **All of the game's prefabs, measured**: each biome's own growths (vines, roots, surtling cores; guck sacks, tar, muddy scrap piles; icicles, ice shards, snowdrifts, crystals, hung deer; seeker creep, eggs, royal jelly, black cores; charred skulls, wall spikes, gems in the walls; frost cores, frozen dead), its own lights, several kinds of chest, the real crafting stations in kitchens, forges and workshops, hero pieces (standing and memorial stones, a wisp fountain, an eternal pyre, a sled, a crane), biome corridor kinds (mud, barricade, creep, icicles, spikes, roots, traps), windows in Ashlands walls, and Ashlands and mountain gates. Props that stood at the wrong height (the fallen statue's parts floated metres up) now stand correctly.
  - Lights and decay use their own random streams, so turning Light or Ruin changes nothing else about a dungeon.
- Internal code cleanup; no change for users.

## v1.41.0 — 2026-10-14

### Added
- **A placed dungeon gets the entrance of its kind on the ground.** **Place in the world** now puts the game's own dungeon entrance for the biome (a burial chamber's mound, a sunken crypt's door, a Frost Cave's mouth, an infested mine's, a Morgen hole) on the ground at the middle of the view, facing you, and the dungeon 5000 m above it, laid out where the game lays that entrance's own (a Frost Cave's at the middle of the zone, as the game does). Its door leads up and the door up there leads back out, as in the game's dungeons. Where the biome has no dungeon of its own (the Plains), the portal pair stays. One dungeon fits over a zone: with one there already, nothing is placed and the editor says so.
- **Go to the dungeon and Go to the entrance.** The view no longer jumps 5000 m up when a dungeon is placed: it stays at the entrance, and two buttons under Place in the world take it up into the dungeon and back down. The Dungeon panel's **Go to entrance** does the same for any dungeon of the area, next to Go inside.
- **Up to 16 levels.** Dungeon levels went up to 4; the stairs now go down as far as 16.
- **Dungeon blueprints.** A blueprint can be a dungeon: switch **A dungeon** on when saving it in the Workshop (or in the Blueprints tab's Details; it is the tag `dungeon`). Pasted into a world from the Blueprints tab, it goes 5000 m up behind the entrance of the biome's dungeon where you click, as a generated dungeon does. A portal in the building marks where players arrive and the way they face (the entrance's door out opens there); generated dungeons opened in the Workshop carry that portal and the tag already.
- **Claude's `generate_dungeon`** places the entrance the same way, and takes up to 16 levels.

### Changed
- Internal code cleanup; no change for users.

## v1.40.0 — 2026-10-14

### Added
- **Moved objects stay the same objects in live mode.** An object moved or turned in the editor (the Select tool's move, or a Cut then pasted) now moves in the running game under its own id when nothing else about it changed, instead of being removed and made again. Mods that keep track of objects keep knowing them: a chest's label sign from a sign mod is no longer doubled after a live move, and a chest keeps what is in it. Undo after applying moves it back. When a player has both its old and new place loaded (a player's game would not show a standing object moving), it is made again as before, and the message after Apply live says how many. Docs: [Moving things](docs/live-mode.md#moving-things).

### WorldEditorBridge
- `/objects` takes objects to move: each moves under its own ZDOID (sent to every player at once), or is made again at its new place where a player has both places loaded. `/status` lists `"features":["move"]`, and editors only send moves to a plugin that lists it. Mods' listeners get the moves in `AppliedChanges.Moved` (`AppliedMove`: where the object stood and stands now), and the old `AppliedChanges` constructor stays.

## v1.38.0 — 2026-10-14

### Added
- **Cut and paste.** **Cut (Ctrl+X)**, beside Copy in the Area tool's **Copy and paste** action and on the Select tool's selection, makes the same copy as Ctrl+C and then takes the copied objects out of the world as one undo step, so a paste moves a build instead of doubling it. The ground where it stood stays as it is. Docs: [Copy and paste](docs/area.md#copy-and-paste), [Select](docs/select.md).

## v1.37.4 — 2026-10-14

### Fixed
- **Spikes and an orange wedge in the 3D view after a visit to the map.** After opening the map and coming back, the spawn-point and nest markers, the Dungeon tool's marks and the Mountain tool's preview could tear models into spikes (gone while their kind was hidden, back when shown) and draw an orange wedge across the view. They now draw correctly after returning from the map.

## v1.37.3 — 2026-10-14

### Changed
- **A click finds an object by its drawn shape.** A chest, a stone or any object under a tree's branches could not be clicked because the box round the tree's whole crown was in the way. Clicks now follow the actual shape of the models, so what you see under the pointer is what you get. Leaves, bushes and other cut-out foliage count only where nothing solid is behind them along the click, and an object whose model is not loaded yet is still its stand-in box.
- **A standing tree is outlined by its trunk.** The selection outline (and the one under the pointer) of a standing tree is now a full-height column round its trunk instead of the box round its whole crown. Logs and stumps keep their box.

## v1.37.2 — 2026-10-14

### Changed
- **A large selection is marked by one box.** Past 500 selected objects the 3D view draws one orange box round them all instead of one round each, since a whole stacked structure selected at once (tens of thousands of pieces) was a solid orange blob that hid it. Up to 500, the boxes are drawn as before.

## v1.37.1 — 2026-10-14

### Changed
- **The green dot on each unsaved object is much smaller.** It was about 1% of the view's height across (11 pixels at 1080p, 21 at 4K) and covered small objects; it is now 5 pixels at 1080p, 9 at 4K, with a soft edge so it stays round.

## v1.37.0 — 2026-10-14

### Added
- **No limit under the Area tool's Flatten, Raise and Lower.** The **No limit: past the game's ±8 m** switch now shows with those three actions too, the same switch as the one at the bottom of the brush options (one follows the other), so an area can be flattened, raised or lowered past the limit without leaving the panel. The cut and fill line then counts all of it, with nothing "out of reach".

## v1.36.0 — 2026-10-14

### Added
- **The Windows program is signed.** `ValheimWorldEditor.exe` in the Windows package now carries a timestamped code signature from the maintainer's certificate (Certum), so Windows names the publisher in the file's properties and in its "Windows protected your PC" prompt, which goes away as SmartScreen gets to know the signature. How to check a download: [SECURITY.md](SECURITY.md#checking-a-download).

## v1.35.4 — 2026-10-14

### Fixed
- **Live mode could lose a request with "Connection reset by peer".** The game's web server closes a connection after 15 seconds without a request, and a request sent at that moment was reset, making a look at the game or an apply fail. The editor now opens a new connection after 10 seconds unused, before the game closes the old one.

## v1.35.3 — 2026-10-14

### WorldEditorBridge
- The plugin's log says more of each apply, for when a change does not show for every player. The apply line adds how many objects the game knew before and after and how many peers are connected, and a second later a new line says how many of the destroyed objects the game still has and how many other objects of their prefab stand within 10 cm of their place (made again, or there all along). See [What the plugin logs](docs/live-mode.md#what-the-plugin-logs).

## v1.35.2 — 2026-10-14

### Fixed
- **Opening another area erased the history made before it.** Steps that did not fit the new area (all of them, after a paste or any edit in another part of the world) were dropped and could never be undone. They are now kept aside and come back when their area is opened again; only a step partly in the new area still stops older ones from being undone there. The History panel says how many steps are kept aside, and the history file keeps them too.

## v1.35.1 — 2026-10-14

### Fixed
- **Worlds made with generated zones had no locations and no creatures in them.** Worlds made with `WorldCheck create <radius>` (the documentation's, the benchmark's and the demo worlds) never got the start temple, altars, ruins and the like, nor the creatures' spawns, in the game or in the editor. The creator now places every location the seed lays out in those zones, with its stones, chests and spawners on cleared ground, and gives every generated zone what its creatures spawn from. A zone with a dungeon's entrance is left to the game. Worlds made on the editor's **New world** page are not affected, as they leave every zone to the game.

## v1.35.0 — 2026-10-14

### Added
- **Live mode for mods' own plugins (the bridge API)**: a mod's BepInEx plugin can register with the WorldEditorBridge (`WorldEditorBridge.Api.dll`, shipped with it and in the SDK's zip). It is told what the editor applied, right after, on the game's main thread (to put its things together again at once instead of on the next load), and it answers calls on routes of its own. In the editor, a mod's extension gets the game it is connected to (`Host.Live`): which mods registered, and calls to their routes for what only the running game knows; it can also ask for its shapes to be drawn again. The SDK's sample shows both halves. See [Live mode for mods](docs/modding.md#live-mode).

### WorldEditorBridge
- `GET /mods` lists the mods' plugins registered with the bridge and the API version; `POST /mods/<id>/<name>` calls one of a mod's routes. Every apply (`/objects`, `/terrain`, `/zones/reset`) tells the registered listeners what changed. Older editors see no difference.

## v1.34.0 — 2026-10-14

### Added
- **Ground cuts for mod extensions**: an extension can leave the ground undrawn inside outlines it gives for its objects (the mouth of a tunnel, a pit the mod digs while the game runs), so what the mod builds below shows, and a click goes through the hole to what is in it. The SDK's sample mod's pits show how. See [Extensions](docs/modding.md#extensions).

## v1.33.0 — 2026-10-14

### Added
- **Editor extensions for mods (the SDK)**: a mod can ship a small DLL, built against the editor's SDK (`ValheimWorldEditor.Sdk`, attached to each release with a sample), and name it in its `worldeditor.json`. It teaches the editor what a file cannot: reading the mod's packed data in the inspector, rewriting the ids it keeps in pasted copies, drawing what the mod builds in code (a sail from its corners). An extension runs only once you turn it on in **Settings → Mod extensions**, and only that file: an updated DLL waits until you turn it on again. A sample extension of a made-up mod shows every hook. See [Extensions](docs/modding.md#extensions).

## v1.32.0 — 2026-10-14

### Added
- **Unity's cylinder, sphere and capsule**: mods' pieces built from them (as from Unity's cube, quad and plane already) are now drawn.

### Fixed
- **Glass and other see-through materials are drawn see-through**, blended over what is behind them, instead of cut out at half (which hid glass less than half opaque) or drawn opaque. Crystal windows are no longer invisible: the game's own Crystal Wall 1x1 and the mods' glass using its crystal material (PressurePlate's Crystal Pressure Plate) show, and nearly clear glass (VALKEA, CoreWoodExtras, MoreGatesExtended) shows as a tint. GlassPieces' glass is no longer an opaque frosted pane. The game's own see-through materials (its ships' glass, gems, some ice) are drawn see-through too; its other objects look as before. Building pictures blend them the same way.
- Mods' materials keeping their texture under another name than `_MainTex` (`_Albedo`, `_BaseMap`...) are drawn with it (VALKEA's dried grass).
- Mods' sounds and effects (prefabs the game draws nothing of) are no longer offered in Place; those already in a world still show.

## v1.31.0 — 2026-10-14

### Added
- **Linked objects**: objects a mod builds out of others (a ship and the pieces built on it) are drawn where the mod builds them, around their root, turned and leaning with it, instead of in a pile where the game saves them. Such a group is moved, turned, copied and deleted whole: a double click on a part selects it, and any part brings the rest along (the Select tool, copy and paste, Claude's tools). Each pasted copy gets ids of its own, so the game keeps the copies apart. The editor knows no mod: a mod says how its objects link in the `links` of its `worldeditor.json`. See [Linked objects](docs/select.md#ships-and-other-linked-objects) and [for mod authors](docs/modding.md#linked-objects).

## v1.30.0 — 2026-10-14

### Added
- **worldeditor.json for mod authors**: a mod can ship an optional `worldeditor.json` telling the editor what it cannot guess: labels, help and a read-only flag for the data its objects keep (the inspector), prefabs not to offer in Place and the Build panel (its tools, effects, helpers), and groups for its pieces in the Mods tab. See [For mod authors](docs/modding.md) and the [schema](docs/worldeditor.schema.json).

## v1.29.0 — 2026-10-14

### Added
- **Mods' data by name in the inspector**: the values mods keep on objects (ValheimRAFT's `MBParentId`, a mod's levels or owners...) now show by their names instead of numbers. Like the game's own, the names are found in the string literals of the mods' code, read from the DLLs in your Valheim folder and mod manager profiles (never run), and kept until a mod changes. The game's own name still comes first when both have one.

## v1.28.0 — 2026-10-14

### Added
- **Crunched textures read**: textures mods keep crunched (Unity's DXT1 and DXT5 Crunched formats) are now read, so pieces that showed untextured, like OdinsKingdom's stone walls, are drawn with their textures.

## v1.27.0 — 2026-10-14

### Added
- **Modded pieces drawn as in game**: a mod's pieces and objects learned from a live game are drawn with their own models and textures (in the 3D view, the Workshop and the Build panel's pictures), no longer as boxes. The editor finds the meshes, materials and textures by name in the mods' own bundles, which it copies out of their DLLs from your Valheim and mod manager profiles, else in the game's. Meshes of the same name are told apart by their size, Unity's own cubes and planes that mods use are made by the editor, and compressed meshes (some mods' bundles) are read. Meshes a mod builds in code while the game runs (ValheimRAFT's sails) are not drawn, and crunched textures (OdinsKingdom's stone) are not read yet: those pieces show untextured.
- **Mods tab by mod**: the Build panel's Mods tab lists one mod's pieces at a time, chosen in a row of the game's mods with their number of pieces, at most 300 at once (search for the others), so a mod that adds thousands (PlanBuild's plans) neither buries the others nor slows the panel.

### WorldEditorBridge
- New `POST /models`: what the game draws of some prefabs, by the names of their meshes and materials, with their sizes.
- `GET /catalogue` names a prefab's mod as it was installed (its package's name), not after the plugin inside it that added the prefab (ValheimRAFT's pieces were listed under "Inventory Patch Detector").

## v1.26.0 — 2026-10-14

### Added
- **Modded games' objects**: opening a live world now asks the game for the objects its mods add (building pieces, furniture, stations...), which the editor cannot find in Valheim's files. Their objects get their names and can be placed, the Workshop's Build panel lists the mods' pieces on a new **Mods** tab with their cost (whatever tool builds them in game: a mod's own hammer too), and the inspector says which mod an object comes from (when Jötunn is installed). What was learned is kept in `mod-catalogue.json`, so a modded server's world read offline afterwards shows them too. The mods' models are not drawn yet. See [Modded games](docs/live-mode.md#modded-games).

### WorldEditorBridge
- New `GET /catalogue`: every saved kind of object the game knows, mods' ones included, with which mod adds it (asked of Jötunn when it is installed).

## v1.25.3 — 2026-10-14

### Fixed
- **The Cut sliders start at their right end, with no cut.** In the Workshop and the Dungeon tool they used to start at the left, where nothing was cut. Now the right end is off and the left end cuts at the ground (Workshop) or the floor (dungeon), so sliding left removes the building from the top down.
- **A pasted town stands as it was copied.** Pasting a whole town elsewhere used to leave it half collapsed in game, with empty chests and trees growing through the floors. This is fixed by the following changes.
  - Copy now takes only the ground the buildings stand on (under each piece and a metre around, blended into the new place over three metres), so the town sits in the land where it is pasted with its foundations and cellars supported as before. The Select tool's copy does the same.
  - Pasted objects keep everything the originals held: chests keep their contents, signs their text, pieces their builder and health. A copy saved as a blueprint keeps the contents, but not the builder.
  - The copied ground can now go past the game's ±8 m when the new place needs it, so pieces no longer float and fall in game.
  - **Clear the site** no longer digs trenches around foundations set into the ground, and ignores pieces hidden far underground (a mod's crafting stations 10 km down used to make it dig a 400 m pit).
  - A paste that does not fit in the open area is now refused with a message, and its outline turns red while it does not fit. Before, the pieces were placed but the ground and trees there could not be handled.
  - The ground and the objects of a paste now line up exactly (the click point moves by half a metre at most) instead of being up to half a metre apart.
  - Claude's `copy_objects` also keeps what the originals hold.

## v1.25.2 — 2026-10-12

### Changed
- Internal code cleanup; no change for users.

## v1.25.1 — 2026-10-12

### Fixed
- **A ruin you built onto shows whole.** The stones of a ruined tower patched with wood were hidden (they belong to the ruin, under Ruins & structures, which is off at first), so only the wood showed in the 3D view and on the map. Ruin pieces that touch your pieces, and the ones touching those, now count as your building: they appear under **Your buildings** in the 3D view and are drawn with **Show buildings** on the map. The map also has a new **Show ruins** option (off at first) that draws every ruin, fainter.

## v1.25.0 — 2026-10-12

### Added
- **The game's locations are drawn whole.** Parts of a location that are not objects in the save (a crypt entrance's rocks, the trader's camp, a boss's altar, a troll cave's walls, the rune tablets of ruins) now show in the 3D view with **Ruins & structures**. The game rebuilds these parts from a marker each time the zone loads, so they cannot be picked or changed. A part the game only keeps by chance is drawn when it is there at least half the time.

## v1.24.1 — 2026-10-12

### Fixed
- **Windows: objects that came and went, black lines across the window.** With some graphics drivers (older ones, laptops with two graphics chips) the 3D view and the map showed old frames between new ones, and lines crossed the buttons. The editor now draws through Direct3D on Windows, as games do. **Settings › Drawing › Draw with the graphics driver's OpenGL** goes back to the driver's OpenGL (applied at the next start). The log now also says what draws the map, as it already did for the 3D view.

## v1.24.0 — 2026-10-12

### Added
- **Claude changes objects already there.** `move_objects` moves and turns objects as one group (a building keeps its shape; with `ground`, each stays as high above the ground as it was). `set_transform` puts each object exactly (place, turn, tilt, size), `replace_objects` swaps them for another kind, `get_object` gives everything an object is and holds as the inspector shows it, and `set_object_data` changes its values (a portal's tag, a builder, a ward's permitted players, health, fuel, a creature's level…). Objects keep what they hold when moved.
- **Claude finds places in a world.** `list_locations` lists the world's locations (the start, traders, bosses' altars, dungeon entrances, villages…) with the names players use, and `search_world` searches every object as the map's Search does (by kind, items in containers, texts).
- **Claude copies and puts blueprints into worlds.** `copy_objects` makes copies once, in a row or in a ring. `add_blueprint` now also puts a library blueprint into a world's area (not only the Workshop's plot), turned as asked, as one of Claude's steps (History's **Take back Claude's changes** takes it back).
- **Claude weathers and regrows ground.** `shape_ground` runs the Area tool's ground actions over a rectangle (smooth, naturalize, erode, restore, raise, lower), and `regrow_nature` regrows what the game grows there.
- **Scripts move, tilt and snap objects.** Objects carry their turn, tilts and size (`Yaw`, `Pitch`, `Roll`, `Scale`). `Objects.Place` takes `pitch`, `roll` and `snap` (pieces meet the pieces there as the hammer makes them), and `Objects.Move` moves, turns or sizes an object that is there, keeping what it holds, in the script's single undo step.
- **Claude digs along lines, resets zones and makes worlds.** `dig_path` runs the Path tool along a line (a river or moat, a cave roofed with boulders, a ramp, a flattened or smoothed way). `reset_zones` marks zones for reset (or unmarks them) as the Area tool does, `preview_seed` shows what a seed's world will be like (a map, and where the start, traders and bosses will be), and `create_world` makes and opens a new world.
- **Claude stamps, imports heightmaps and masks its ground work.** `stamp` presses a stamp into the ground (Mountain, Mesa, Crater rim, Dunes, Rocky ground, your own, or any PNG), `import_heightmap` puts a heightmap picture into a rectangle, and `shape_ground` can also paint. `shape_ground`, `dig_path`, `stamp` and `import_heightmap` take a mask like the Mask panel's (heights, slopes, biomes, paint).
- **Claude edits the game's dungeons.** `list_dungeons` and `dungeon_rooms` show a dungeon's rooms, open ends and door joints, and `add_dungeon_room`, `remove_dungeon_room`, `close_dungeon_ends` and `set_dungeon_door` change it as the Dungeon tool does.
- **Claude saves blueprints.** `save_blueprint` saves the Workshop's building to the library (as Save blueprint does, with what objects hold and a picture), refusing to replace an existing one unless told to, and `open_workshop` loads one.
- **Claude uses every control of the editor.** `list_controls` lists every switch, dropdown, slider, number and text box and button the editor shows (the View panel's switches, the Workshop's Cut and Support check, each tool's settings, the tool rail) with its value, and `set_control` sets one as you would. Saving, applying live, discarding, reloading, writing or deleting your files and connecting to the live game are refused.
- **Claude reads the ground exactly.** `ground_at` gives the height at points (now and as generated), the biome and any water there, and `list_objects` also gives tilts and size when an object has them.

### Fixed
- **Moving objects on the Workshop's plot** (or a generated dungeon's, before saving) lost what they held, such as a chest's contents or a sign's text. They now keep it.

## v1.23.1 — 2026-10-12

### Fixed
- **Workshop:** a click that moved the mouse a little before letting go placed nothing. It now puts the piece the preview shows.
- **Discard** that has to read the world again no longer fails silently when the world's files cannot be read. It says why and keeps the area open.
- Internal code cleanup; no change for users.

## v1.23.0 — 2026-10-12

### Changed
- **Larger areas.** The area can now be 9 × 9, 13 × 13 or 17 × 17 zones (up to 1088 m across), from the map's size box and the top bar's. Follow grows it up to 17 zones each way, and Claude's `open_area` opens up to 17 × 17. Large areas are slow to open and draw, so they are meant for big works and for testing the editor's speed.
- **Faster drawing of large areas.** Only the parts of the area in view are drawn, and small objects stop being drawn far away (a few pixels across). A 17 × 17 area with 2.3 million objects went from 31 to about 75 frames a second.

### Fixed
- **Unsaved marks** kept their green dots after **Save to world**. They now go once saved, as after **Apply live**.
- **Areas with very many new objects** were slow at every frame (a 17 × 17 area with 2.3 million new objects drew a frame a second or less). The green dots are now kept on the graphics card and rebuilt only when objects change.
- **Scripts on areas with very many objects** waited over a second before running, and after a save of many new objects each change was slow to count what is pending. Both are now fast.

## v1.22.0 — 2026-10-12

### Added
- **A saved world keeps its list up to date.** World folders added or removed while the editor runs (or a world saved again) show up by themselves within a few seconds, without leaving the page and coming back. **Refresh** looks for them at once.

## v1.21.0 — 2026-10-12

### Added
- **Spawn points and creature nests** (the invisible points the game's camps, villages and dungeons spawn creatures from) have their own kinds in the Place list and the View panel, each saying what it spawns (`Boar · 0–2 ★ · spawns once`), and Place's search finds them by creature. In the 3D view spawn points are orange boxes (red when the creature comes back) and nests are purple ones, with a marker above each, seen within 250 m. They are off at first, like ruins. The inspector and Claude's `find_prefabs` say what they spawn.
- **Objects are sorted into real kinds.** Furniture & props, Bones & remains, Creatures & NPCs and Effects & markers join the lists, and **Other objects** shrank from about 470 kinds to a handful.

### Changed
- **Follow grows the area** instead of moving it. When you look near the area's edge, a row of zones is added on that side and the zones already open stay (a 3 × 3 area becomes 4 × 3, then 4 × 4…). It never leaves zones behind and stops at 16 zones each way (about 1 km). The arrows move the grown area as it is, and the size box makes it square again.
- **The tool rail** is in sections, in a clearer order: **Sculpt**, **Edit** (Area, Path, Place), **Paint**, **Create** (Shape, Mountain, Cave, Dungeon, Script, Blueprints) and last **Tools** (Select, Measure, View).
- **The Place panel** is in sections with headings: **Line shape** (Points, Circle, Rectangle, right under the modes, with how to draw it), **Choose a preset**, **Selected kinds**, **Objects** and the mode's own settings. The preset list starts on **None**, and choosing None after a preset unticks its kinds. The selected kinds sit in a box on rows of one layout, each with its share (100% for one kind), and **Choose kinds…** opens the list under them. The **Recent** row also shows the kinds chosen now, and its **all** / **none** buttons are larger. **Tilt** starts at 0.
- **Place's One at a time** places only the object at the cursor. A drag no longer scatters more, and Density, Spacing and Clumping hide while it is on.

### Fixed
- **Kind names with an underscore** (Pickable_Flax) showed without it in the Choose kinds list and on the Favourite and Recent buttons (PickableFlax).
- **The Place panel's weight sliders** stuttered during a drag and now run smoothly. Each slider is now the kind's share in %, and the other kinds' sliders move with it, keeping their proportions, so the shares always make 100%. An equal share sits in the middle of the slider, whatever the number of kinds.
- **Previews over the sea** (the mountain, placed and pasted objects, rings and lines) were hidden wherever water was behind them in the 3D view. They now show over the water.

## v1.20.2 — 2026-10-12

### Fixed
- **Location markers** no longer put a post on every invisible ground disc of No limit (one every 6 m where the ground was taken past ±8 m), which hid the real locations. The "location(s) here" note no longer counts them either.

## v1.20.1 — 2026-10-12

### Changed
- **While the game's look is prepared** (start page), every action is greyed out until it is done: worlds, servers, the Workshop, a new world and Settings. A world opened meanwhile missed the game's look. The side menu still moves between the pages.

### Fixed
- **Rocks that shape the ground** (the Plains' tall rock pillars, coast, heath, forest and copper boulders) were missing from the 3D view and the object lists. They now show and can be selected like other rocks.

## v1.20.0 — 2026-10-11

### Added
- **The start page has a side menu**: **Home**, then **Edit a world** (my game, a dedicated server, a saved world) apart from **Create** (the Workshop, a new world). Home shows the game when it runs, with **Edit live**, and what you opened last: saved worlds, blueprints (with their pictures) and the servers that connect in one click. The Workshop's page lists every blueprint to open.

### Changed
- **The world map** of a world opened again (Reload, or back to the worlds and in again) shows at once, because the last world's map is kept instead of being worked out again (which took seconds).
- **The editor now reads from your Valheim** how the game's locations shape the ground, the game's objects (what new ones need in the save, containers, wards, saplings, what stands hold), the build pieces (footprints, snap points, build tools, what holds them up, how the hammer places them), the names of object data, the cave roof rocks' shapes, what pieces cost, the vegetation rules Regrow nature follows and the dungeon rooms the Dungeon tool joins from. Before, it kept its own copy, which went out of date with game updates. Now they always match your version of the game, and reading takes a few seconds the first time and after a game update. Valheim is now required: without it, no world or Workshop opens (the start page asks for its folder).

### Fixed
- **The start page** said "Preparing the game's look" at every start, then "The game's look is ready" for the rest of the session, even when the editor had only checked what it kept. It now says so only when it really reads the game's files (the first time and after a game update).
- **Settings > Claude** could still say the editor could not listen on its port after Claude was turned off. The old message is now cleared when the switch is off.
- **Script tool:** Ctrl+Enter in the code box runs the script again, as its button says.
- **The world map**, zoomed out a little from the closest views (3 to 6 m a pixel, on a wide window), kept a processor core busy for nothing. This is fixed.
- **Preparing the game's look** (start page) shows how far it is and what it reads, one game bundle at a time. Before, the bar stood still at the start until the look was ready.
- **The world map** no longer holds up the rest of the editor while it is built. It used to take every background thread, so on a computer with few cores a live game's players, its answers and other background work waited seconds for it.
- **Damaged or tampered files are refused cleanly.** A save, live snapshot, blueprint or heightmap picture with wrong lengths or counts, damaged compressed data or a broken PNG header now says it is damaged. Before, it could fail with an unrelated error or ask for gigabytes of memory.
- **Generate dungeon** could offer the dungeon made for the previous settings. Changing a setting while the last one was still being made, then placing it, placed the old one (and **Open on the plot** then opened another one). A dungeon made for settings that have since changed is no longer shown.
- **My game** (start page) now says when your game could not be checked (a folder that cannot be read, for instance), and the log has the error. Before, the card just stopped changing.
- **The history kept on disk** failed when the world's saves were removed while it was open (the game or another tool cleaning the folder). It is now kept without comparing it to a save.
- **Zone grid** (map) on a scaled screen (200 %, for instance) showed from twice as far out, with its lines cramped together. It now shows from the same zoom on every screen.

## v1.19.0 — 2026-10-11

### Added
- **A new world** (on the start page) makes a normal new world that players join without any mod (crossplay included). The seed's world shows before it is made (on **Generate world**, Enter or **Roll**): its land and biomes, and exactly where the game will put the start temple, the bosses' altars and the traders' spots (Haldor, the Bog Witch, Hildir), with how far each is from the start and whether you need to sail to each biome. **Find a seed** tries many random seeds and keeps the best, listed to fill the window, for what you ask: the start on a large landmass, land around it, Swamp, Mountain or Plains within a distance, every early biome on foot, a trader or the first five bosses within a distance. The map zooms (wheel, drag, double click) and stays sharp, switches in its corner show or hide each kind of mark, and the seed's facts are listed beside it. The **Dungeons** switches show where the burial chambers, troll caves, sunken crypts, frost caves, infested mines, charred fortresses and Hildir's dungeons will be, with how many lie near the start. The rules are read from your Valheim, so they follow game updates. See [docs/new-world.md](docs/new-world.md).

### Changed
- **The world generator is about a third faster** (world map previews, Regrow nature, new worlds), with exactly the same results.

### WorldEditorBridge
- **A failed request's log line** now stays on one line. Line breaks in it could make it look like several lines in the game's log.

## v1.18.0 — 2026-10-10

### Added
- **Building skill for Claude Code** (`.claude/skills/valheim-build`): a workflow for buildings and places that look hand-made. It starts with research and a design brief, then mock-ups of unfamiliar pieces, phased building with a helper library, checks for blocked windows and other mistakes, then interiors and landscaping. See [docs/claude.md](docs/claude.md#the-building-skill-claude-code).
- **Claude can work in the editor** (Settings → **Allow Claude to connect**): an MCP server inside the editor, on this computer only and with a secret token, for Claude Code or any MCP client. Claude looks at the open world or Workshop (its state, heights, biomes, objects, screenshots, the game's pieces), shapes it with scripts, puts down building pieces (snapped as the game's hammer does), checks that they stand, adds blueprints and generates dungeons. It is told how the editor and Valheim's building work when it connects. It builds floors, walls with doors and gable roofs over rectangles, levels ground, lays roads, paints and plants forests in one step each, fills chests and writes signs, sees four views of a place at once or a map of the area with coordinates, and works on what you selected. What it changes stays pending, one undo step each: it never saves, applies live or saves a blueprint, and changes nothing while live with Auto on. Claude Desktop connects through `ValheimWorldEditor --mcp-stdio`. **History → Take back Claude's changes** removes all of Claude's changes and keeps yours. See [docs/claude.md](docs/claude.md).
- **Item stands and armour stands can be filled.** The Inspector's **Holds** puts an item on an item stand (a weapon, shield, trophy or fish on a wall, food and potions on one lying flat to set a table) with its quality, style and the way it hangs. **Wears** dresses an armour stand slot by slot (armour, cape, belt, shield, weapon) and sets its pose. Each list offers only what the game lets that stand or slot take, and the item is saved whole, as the game saves it, so it comes off the stand as set. Claude does the same with `set_contents`.
- **Generate dungeon in the Workshop.** The Build panel's Library has **Generate dungeon…**, the Dungeon panel's generator (building pieces only), with **Open on the plot** (the dungeon alone) and **Add to the plot** (with what is there, one undo step). Its blueprint is kept in the library, with what its objects hold.

### Fixed
- **Serving tray food stays on the table.** Food and drink of the serving tray (Feaster: dishes, meads, berries, the feasts) placed, pasted or added with a blueprint in the editor are now marked as pieces, as the game marks them. Before, they were loose items in game that fell and despawned away from a base.

### Changed
- The Windows package holds `ValheimWorldEditor.exe` with its four native libraries beside it (keep them together), and the program now carries its details (name, description, author). Some antivirus programs flagged it, because it unpacked those libraries to a temporary folder at each start, which looks like what a dropper does, and it had no details.

## v1.17.0 — 2026-10-10

### Changed
- The game's look (models, textures, the terrain shader and the map) is **read straight from your Valheim**, in the editor itself, and nothing is copied any more. There is no more multi-minute copy at the first start and after every game update (the editor reads the game's files for a few seconds the first time and after an update), the 150 MB `game-look` folder is gone (the old copy is deleted), and release packages are smaller, with no Python inside: about 23 MB less to download on Linux (77 MB less unpacked) and 10 MB less on Windows (37 MB less unpacked).
- The editor runs on **.NET 10** (it was .NET 8, whose support ends in November 2026). There is nothing to install: the packages still carry it.

### Added
- When Valheim is not found, the start page explains step by step how to find its folder (Steam's **Manage › Browse local files**, the usual places on your system). A folder next to the game folder (`steamapps`, `common`, `valheim_Data`) is taken as the game folder it leads to, on the start page and in Settings.
- Live: the open area **follows the game**. Every 2 seconds the editor asks the game what changed in that area's zones (only those). Objects players built, removed or changed appear, go or change in the editor, and ground dug or paved there comes in, without steps in History and without dropping your changes (Reload is no longer needed for the area you work in). A changed object stays the same object (history, selection and inspector follow it). Objects you deleted and applied stay deleted, and objects the game removed are not brought back by undo or redo.
- Live: where you have ground changes not applied yet and the game changed that zone's ground too, yours are kept, the status bar says so, and **Apply live** asks first, because applying could erase what players did there. Undo your changes there and the game's ground comes in.

### Fixed
- Place, Line: a line exactly a whole number of spacings long sometimes left out its last object.
- Opening another world while the map or an area of the first was still being drawn no longer takes that world's rivers and streams away (its ground came out without them), and the map reads its own world's biomes. Making a world's ground while Regrow (or another world) runs no longer shifts its rivers and streams away from the game's.
- Ground is worked out a little faster.
- A blueprint or copy with a damaged object value (a number that is not one) no longer makes saving the world fail. That value is left out when the blueprint is read.
- In the Workshop, the pieces of a ruin blueprint (a generated dungeon) added to a building now stay a ruin's in the saved blueprint, instead of becoming the builder's.
- The 3D view no longer stalls for a frame after each Dungeon tool change while it reads the dungeons' rooms again.

### WorldEditorBridge
- `/watch` gives one number per asked zone, which changes when an object there is made, removed or changed (not when a creature walks or a fire burns). `/zone` gives the saved objects of the asked zones, portals included, in the snapshot's format. Only those zones are read, from the game's own per-zone lists.

## v1.16.0 — 2026-10-09

### Added
- **Generate a dungeon** (Dungeon panel): a whole dungeon from a few choices and a seed, made of building pieces or of the biome's own dungeon rooms. A plan of each level updates as you change the choices.
  - Eight styles: crypt, catacombs, temple, fortress, prison, Dvergr hold, goblin warren and ruins. Each has its own rooms (burial chambers, ossuaries, chapels, barracks, feast halls, cell blocks, forges, studies...), each furnished for its purpose.
  - Seven wall materials, and eight biomes for monsters, chests, loot and lights.
  - Settings for size, levels, monsters (spawners that come back, or monsters placed once), the boss, loot, light, furniture, ruin and ways round.
  - The layout reads like a building: rooms along axes, wings in pairs, and loops between rooms that lie close together. Stairs go down between levels, and some halls rise through two levels.
  - A door closes every room, so its monsters wait behind it. An antechamber leads to the boss's arena behind a gate that opens with the crypt key, which lies in a chest far away, and the treasury is beyond the arena. Hidden caches sit behind cracked walls.
  - **Place in the world** puts it 5000 m above the view with a linked portal pair, as one undo step, written as a ruin like the game's own dungeons. **Open in the Workshop** opens it as a blueprint.
- **Blueprints keep what objects hold**: chests' contents, signs' texts, creatures (a dungeon's boss, with its stars) and whether the building is a ruin. This is stored in extra lines of the file that Homestead and PlanBuild skip, so the file still builds in game. It comes back when the blueprint is opened in the Workshop, saved from it, or placed in a world.
- **Chest contents as in game** (inspector): the container's slots are shown in a grid. Click one to choose its item, how many and how good, empty it, or move it to another slot. A double click on an object opens its inspector.
- **Dungeon tool**: edit the rooms of Frost Caves, burial chambers, sunken crypts, infested mines and the game's other dungeons. The game builds a dungeon from a list of its rooms kept in the save (5000 m above its entrance). The tool edits that list and the game builds what you make, no mod needed.
  - **Go inside** takes the view up to the dungeon and cuts its roofs away to look into its rooms.
  - Pick one of the dungeon's rooms and click an open end (a green frame) to join the room there the way the game joins rooms. **Turn** (R) joins it by another of its openings. A red box warns when it would overlap another room or leave the dungeon's space.
  - Added rooms come with what the game would put in them at that place (chests, creature spawners, torches, ice, decorations), rolled the way the game rolls them.
  - **Doors** where rooms meet: an added room gets the dungeon's door (curtain, gate, ice wall) by the game's chance, and a click on a square where two rooms meet puts one there or takes it away.
  - Delete a room (its contents and doors go with it), and **Close open ends** caps them with the game's end caps. Each change is one undo step, and **Save to world** and **Apply live** write it.
  - **Build here**: build with the hammer's pieces inside the dungeon, on its rooms' floors (where they hold in game).
- The game's look now includes the dungeons' rooms (copied once more after this update).
- **Dungeon rooms** in the View panel shows or hides them.

### Fixed
- Contents set in the inspector on a chest never opened were replaced by the game's own loot when it first loaded. Such a chest is now marked as already filled.
- The support check now knows the pieces the game never checks: Mistlands' large black marble floor (also carts, ships and wards) stays up wherever it is and holds what stands on it, as in game. A building on such floors in the air no longer shows walls that would fall.

## v1.15.6 — 2026-10-09

### Fixed
- Saving a world the game saved since the editor read it (a play test between two saves) no longer throws away what was done in the game. Before, leaving the world deleted the game's save. The editor now says Valheim saved the world and saves nothing. Leave the world and open it again to edit the game's save.
- Changes saved (or applied live) from the map are kept as saved. Before, **Discard** in the area opened next undid them too, saving again took them out of the world, and the History panel showed them as not saved.
- **Area → Backup** no longer restores another world's backup. A folder chosen with "Another folder…" stays in the list when another world is opened, and choosing it there used a backup read for the first world without checking its seed.
- Live: ground changed while **Apply live** waits for the game (a second stroke with Auto apply on) is no longer lost. It used to be marked as applied but never sent, so players did not see it and Discard did not take it back. It now stays pending and goes with the next apply.
- The editor's keys (Ctrl+Z, Ctrl+Y, Ctrl+S, Ctrl+V, the tool keys) now do nothing on the start page and the map. On the map they used to act on the area left open behind it: Ctrl+Z undid its last step unseen (live with Auto apply on, in the game too).
- A world with two terrain objects in one zone (the game makes them now and then) can be saved. Every save used to fail with "An item with the same key has already been added". The zone's ground now goes into both, so the game shows it whichever one it uses.
- A save cut short (the game or the editor stopped while writing it: no `_main.<n>.ok`) is no longer taken for the world's save, as the game does. The world opens from the last complete save, and leaving it no longer deletes that save. Leaving a world also no longer deletes your own files named `_main.<something>` in its folder (a `_main.backup.zip`).
- Copying the game's look no longer breaks for good when it is stopped halfway (closing the editor during the first copy). Before, it could leave a cut file that made every later copy fail, or models without their textures that were never copied again. Each file is now written whole or not at all, and a model counts as copied only with its materials.
- After a Valheim update, the game's look is copied from the new game files. Before, the copy kept using where things were in the old ones (new kinds got no model, changed ones kept the old model, and a removed file made the copy fail every time). It now reads the game files again and copies every model again when they changed.
- Settings and saved servers are written whole or not at all, so a crash or power cut while writing them no longer resets the settings (Valheim folder, world folders, recent worlds) or loses every saved server. Settings that cannot be read are kept as `settings.json.bad`, and a settings file that cannot be written (locked) no longer stops the app.
- `servers.cfg` is only readable by you from the moment it is made (it was readable by others for an instant), and a saved password with spaces at its ends no longer loses them (the login failed).
- Opening another area (or saving, or switching the look) while the models are still being read no longer closes the editor or draws objects of the area left.
- An error the editor did not expect no longer closes it and loses every unsaved change. It is written to the log and shown in the status bar (or a message away from the 3D editor). Saving, applying and Discard from the map, and leaving a world, now say what went wrong (a world folder gone or locked) instead of closing the editor or staying on "Saving…".
- Blueprints: importing (also by dropping a file on the 3D view), exporting, deleting and turning an older blueprint into a Homestead one now say so when the folder cannot be written, instead of closing the editor. The same goes for exporting a heightmap.
- Opening a blueprint in the Workshop that cannot be read (removed meanwhile) keeps the open world as it was. Before, the world was closed first, and the editor kept showing an area of it that could no longer be saved or applied.
- A folder of worlds added in Settings that holds a folder that cannot be read (a drive's root, with its "System Volume Information" or "lost+found") no longer leaves the start page with no world listed. That folder is skipped, and so are unreadable mod manager profile folders.
- Shape formulas: a function given the wrong number of values (`sin()`, `pow(x)`, `clamp(x, 0)`) is now a mistake explained when the formula is read, instead of every point failing as "bad". A formula nested very deeply (a pasted one with thousands of brackets) is refused instead of closing the editor.
- A damaged history file (kept between runs) no longer stops its world from opening: the world opens without the earlier history. A `stamps.json` with an entry missing its picture no longer stops the editor from starting.
- Valheim for Windows: when the terrain shader of a copied game look cannot be converted on this computer, the start page says so, instead of copying the whole game look again at every start (minutes each time, ending the same way). A later editor whose shader converter changed makes the shader again from the copied file, without a new copy.
- An object put (or pasted) outside the world, beyond about ±16 km, is now left out of the save and listed under "Not saved". Before, the whole save failed.
- The Mask's slope, height and paint rules now judge edits made at once (Mountain, Shape, Paste, Stamp once, Path) on the ground as it was before them. They used to see the points already changed by the same edit, so with a slope limit most of a mountain was left out in a ragged pattern.
- Copies and blueprints keep objects of kinds the editor has no name for (from mods). Before, they came back as another, unknown kind and were left out when pasted.
- The Area tool's **Restore** also takes back No limit ground (a mountain, a deep paste), as the Restore brush does. Over a mountain it used to change nothing visible.
- A **Path** with Smooth along the west or east edge of the area no longer mixes in the ground of the opposite edge.
- Pointing past the edge of the area no longer finds "ground" 1000 m down. The Measure tool, pastes, brushes and the Path and Area tools used to take such points. Ward and workbench rings crossing the edge no longer plunge down outside it.
- An object found with the map's search and opened with **Edit in 3D** now stays selected in the editor. Before, the selection went at the first frame drawn, while the inspector still showed the object.
- **Script**: a script's changes are not applied when the area changed while it ran (a stroke, an undo, a save that read the world again). They used to overwrite the stroke, or remove other objects than the ones the script chose. Also, an object number that is not the area's no longer leaves the script's ground change without an undo step while saying nothing changed.
- Closing the window with unsaved changes and answering **Keep editing** no longer stops the copy of the game's look running in the background.
- Live, SSH: a saved server that uses a key file with a passphrase can connect again. Its row has a **Key passphrase** box (the passphrase is never saved), where before the connection always failed with "The key file could not be read". One of the usual keys in `~/.ssh` that cannot be used no longer stops the login before the password is tried. The server's remembered identity is now checked for every account on that address (logging in as another user used to accept any identity and save it).
- The README in the editor's packages names the plugin's file as it is on the release page (`WorldEditorBridge-1.15.6.zip`; it said `WorldEditorBridge-v1.15.6.zip`).
- The world generator is closer to the game's: lengths and distances between points (rivers, streams, lakes) are worked out with the game's precision. Of the heights recorded in the game, 2992 of 3000 now match bit for bit (2982 before) and all 8 zones (6 before), and the rest still differ by less than a millimetre.
- The map and areas are made faster on computers with many cores: the ground of 256 zones takes about 0.1 s instead of 0.6 to 1.3 s.
- Areas with very many objects of one kind (tens of thousands of one piece) open, save and switch the look without a pause of a second or more.
- Worlds open and save faster. A world of 355,000 objects and 78 edited zones reads in 0.24 s instead of 0.57 s, and worlds with thousands of edited zones gain much more.
- Less memory over a long session. Models that pile up with every new kind seen (gigabytes with Valheim open beside it) are now let go, and past 1 GB of models and textures on the graphics card, opening another area lets them go (that area's are read again).
- Areas with hundreds of kinds of objects no longer hold up other background work while their models are read (the next area, the map's search and close-up used to wait seconds).

### WorldEditorBridge
- An apply the game takes more than 30 seconds to start (a world save on the server) is no longer done later anyway. Before, the editor was told it failed and sent it again, so new objects were made twice for every player. Now, if given up on, it never runs, and once started, the editor waits for it.
- Objects linked to others (a creature spawner and what it spawned, two connected portals) keep the link when a live delete or move is undone. Before, the object came back without it (a spawner then spawned a second creature) and the other side still pointed at the removed object. A paste of a linked object still there takes no link (it would take the original's).
- A malformed object to make now fails the whole call before anything changes. Before, objects removed and made ahead of it stayed done, and sending the call again made them twice.

## v1.15.5 — 2026-10-09

### Fixed
- Copying the game's look works with Valheim for Windows. It used to stop with "ValueError: 15 is not in list", because the Windows game has its terrain shader for Direct3D and Vulkan only. It now takes the Vulkan one there and turns it into the same shader, so the ground looks as it does from a Linux game.

### Added
- **Open log**, at the bottom of the start page and when copying the game's look fails, opens the editor's log, to attach to a bug report on GitHub.
- The log is now `ValheimWorldEditor.log` (was `log.txt`), started over at each run like the game's `LogOutput.log`, and says more: each line has its time, and it holds the game-look copy's own output (the error that stopped it, too), which Valheim folder and game build it copied from, and errors in background tasks.

## v1.15.4 — 2026-10-09

### Fixed
- Undoing a placed object after **Apply live** now removes it for the players too, on servers with mods that make objects again under a new id when they appear (ServersideQoL does, for build pieces, plants, fires, turrets and more). Before, the undo found the object "already gone" and players kept seeing the new copy. Each removal now also says what the object is and where it stands, and the plugin removes that object at that place when its id is gone. This needs the plugin from this version on the server.

### WorldEditorBridge
- An object to remove whose id is gone is looked for by its kind and place (within 10 cm), once each, and removed if found. Another kind of object at that place, or an empty place, is left alone. Editors from before still work as they did.

## v1.15.3 — 2026-10-09

### Fixed
- The editor no longer fills the graphics card's memory over a session. Each trip between the map and the 3D editor used to leave the whole area's models, textures and ground on the card (about 165 MB a trip), and each move to another area left its ground and game look (about 40 MB). Both are now deleted, so the editor stays near the same size however long it runs and leaves room for a game played beside it.

## v1.15.2 — 2026-10-09

### Added
- The history is kept after the editor closes. After each **Save to world** or **Apply live** it is kept on disk with the world, and when the world is opened again (in a later run too) its steps are back in **History**, tagged **earlier session**, ready to undo: the ground as it was, a deleted object back with its contents, a placed one removed. When the world may have changed since (live, or saved by the game), the editor asks once before undoing or redoing those steps.

### WorldEditorBridge
- A new mod page explains what people use live editing for, how it works, what to expect, and what each connection message means. The plugin itself is unchanged.

## v1.15.1 — 2026-10-09

### Changed
- **Blueprints** on the tool rail is now a tool like the others: it lights up, and the blueprint list opens next to the rail in place of the last tool's options (it used to open on the right, over the View panel). **Blueprints…** in the Area tool opens it the same way.

### Fixed
- The saved-world page no longer says each save makes a full backup (it has not since 1.14.0). Each save is read back and checked before the old one is removed.
- The server form now says the plugin's token is written the first time the server starts with the plugin. The README, the plugin's README.txt and its mod page say so too, and the start page pictures show the Workshop.

## v1.15.0 — 2026-10-08

### Added
- The area's size (3 × 3, 5 × 5, 7 × 7 zones) can be changed on the fly next to the Area arrows, around the same middle zone, keeping the view, the changes and the history.

## v1.14.1 — 2026-10-08

### Fixed
- Zooming out no longer fogs the whole area over. The fog starts at the point the view turns around, so only what lies beyond it fades.

## v1.14.0 — 2026-10-08

### Changed
- **Save to world** (offline) now works like Apply live: the area and its history stay after saving, and you can undo a step and save again to take it back. No backup folder is made at each save any more. Nothing of the old save is removed before the new one reads back right (on a failure the new files go and nothing changed), and the save the world was opened from stays until the world is left.

## v1.13.0 — 2026-10-08

### Added
- **Mountain** shows a preview before you click: the mountain under the pointer as a mesh on the ground, as high as it will rise. The circle turns red where it does not fit in the open area.

### Fixed
- A wide brush or Mountain circle no longer drops a line straight down where it passes the edge of the open area, and it follows hills instead of cutting through them.
- Zone borders (and the other overlays lying on the ground) now follow the ground when it changes. After undoing or discarding a mountain they no longer outline it in the air.

## v1.12.1 — 2026-10-08

### Fixed
- Buildings copied with the Select tool keep their shape when pasted on a slope. Each piece no longer follows the ground on its own (trees, rocks and the like still do).

## v1.12.0 — 2026-10-08

### Added
- **Paste** shows what it will place as see-through models (not only posts), at its height. Ctrl + wheel lowers or raises it (into a hill, Clear the site digs it out).

## v1.11.0 — 2026-10-08

### Added
- In a world, a **Blueprints** button on the tool rail opens the library, to paste a blueprint made in the Workshop.
- **Clear the site** (Paste, on by default): the ground in a pasted building's way is dug down to its lowest piece (a metre around too, never raised, past the ±8 m if need be), and the trees, rocks, bushes and pickables there are taken away, all in the paste's undo step.

## v1.10.0 — 2026-10-08

### Changed
- A blueprint opened or added in the Workshop now stands on its lowest buildable piece (rocks and other things the hoe places do not count). The support check takes the ground it stood on in game to be under its lowest piece at each spot, or at Homestead's terrain contact points. Rocks are left out of the support check.

## v1.9.1 — 2026-10-08

### Fixed
- Objects were shown untextured (white) after opening another area while models were still loading.

## v1.9.0 — 2026-10-08

### Added
- **Cut** (the Workshop) shows the building only up to a height, a metre at a time, and the cursor goes through what is hidden, so you can build inside it.

### Changed
- The **support check** now tints the pieces in the game's build-mode colours (light blue on the ground, green to red) instead of outlining them, and the pieces' own look still shows.
- The Workshop draws pieces solid (See-through buildings is for worlds).

## v1.8.0 — 2026-10-08

### Added
- The Workshop's **Library** tab shows your blueprints with pictures, cost and search. **Open** puts one alone on the plot, **Add** adds it, you can drag one onto the plot, or drop .blueprint and .vbuild files from your files to import them into the library. A blueprint keeps its form exactly, and Homestead's keep their anchor's height when saved again.

### Changed
- The start page's Workshop card opens the Workshop, and the blueprints are in its Library.
- There are no unsaved marks in the Workshop (everything on the plot is new).

## v1.7.0 — 2026-10-08

### Added
- **Select**: Ctrl + click adds to the selection, and Shift + click takes the row from the last object clicked (wall 1, Shift + click wall 3: walls 1 to 3).

## v1.6.0 — 2026-10-08

### Added
- The Workshop places pieces where the **game's hammer** would, using the game's own data for every piece (colliders, snap points, placement rules). The piece touches what you point at, then snap points within half a metre meet, and pointed at a wall's top, the next stands on it.
- **Ctrl + wheel** lifts the piece (Shift: 0.1 m), and , and . turn it by the game's 22.5° (or another step).
- The Build panel's **Plants** (cultivator) and **Feasts** (serving tray) tabs: everything players build.

### Fixed
- Turning with the wheel now takes one step a notch (smooth wheels and touchpads too), and a snapped piece keeps the turn asked for.

## v1.5.0 — 2026-10-08

### Changed
- The Workshop keeps to building: **Build** (the hammer's pieces in its tabs, with pictures, by crafting station and name, searchable, with snap, grid and turn step), **Select** and **View**. There are no world tools, Mask, View panel or zone borders there.

## v1.4.0 — 2026-10-08

### Added
- The blueprint library shows each blueprint's **3D picture** (the game's models when its look has been copied), its **cost** in game (materials and crafting stations), a **description** and **tags**. The search looks through all of them, and **Details** changes them.

## v1.3.0 — 2026-10-08

### Added
- **The Workshop**: a blank, flat plot (from the start page or the Blueprints panel) to build a building on, then **Save blueprint**, which keeps only its building pieces. Blueprints open in it to be changed.
- **Support check** (in the Workshop): each piece's structural support worked out by the game's own rules (per material), from full on the ground to what would fall. Saving asks first when some pieces would fall.

## v1.2.0 — 2026-10-08

### Added
- Blueprints are now **Homestead**'s (the in-game building mod): they are saved into its folder, so they show in its hammer tab ready to build in game, and its own blueprints are listed here. The editor finds Homestead in the game's BepInEx and mod manager profiles, and warns when it is not installed.

### Changed
- **Save blueprint…** now writes Homestead blueprints (pieces only, no ground). Blueprints kept in the editor's own format before are still listed, and **To Homestead** moves them.

## v1.1.0 — 2026-10-08

### Added
- **No limit** (in the brush options; for every ground tool: brushes, Path, Area, pasting) moves the ground past the game's ±8 m, for mountains, cliffs and canyons. Saving (or Apply live) turns that ground into invisible ground discs (the game's own DevGround1 location) that every player's game counts as generated ground: no mod needed, console players included. The hoe and pickaxe then get their ±8 m from the new ground. The disc heights are fitted by working out the ground exactly as the game does, what they leave is ordinary ground edits, and the discs' dirt is painted back to the biome's ground. Trees, rocks, ore, bushes and pickables where the ground moved more than 2 m are taken away, and building pieces stay. Lifting again near earlier discs works them out again, and zones the game has not generated yet are left as they were. Very steep walls come out softer, more so near the world's middle (the game applies the discs in an order that depends on their height there).
- **Mountain** tool: a click raises a lone peak, a ridge, a mountain range, a mesa, a volcano or rolling hills, past the ±8 m (ground discs on saving). Each is made from a seed, with a natural profile (steeper up high, easing out at the foot), a ragged outline and broad ridges and gullies, and slopes mostly stay under 35°, so they keep grass and trees. Randomize rolls a new one of the preset (size, roughness, direction, seed) that fits in the open area. It takes away the trees and rocks it buries and grows the biome's own on its slopes, by the game's rules.
- **Cave** tool (the Path tool's new Cave action): along a drawn line, a trench with an entrance slope at each end, roofed with the game's boulders (forest, coast, heath or mountain rocks, by biome or chosen). They are placed upside down so their flat side is a nearly level ceiling, turned, tilted and sized at random, and each is hung by its real underside to clear the headroom. More are added until the floor is covered, and the walls rise into the rock so no daylight comes in at the sides. A line drawn as a ring makes a ring-shaped cave with one entrance. There are presets (tunnel, cave, cavern) and Randomize. The ground cannot overhang in Valheim, so the roof is rocks, which players can mine like any boulder.
- **Script** tool: C# scripts run on the open area, to generate anything the tools do and more. The editor compiles them itself (no .NET install needed) and gives them Area (its corners, its points, sea level, the world's seed), Ground (Height, Original, Set, Raise, Lower, Paint, Biome, Shape, Mountain; past the ±8 m by default), Objects (All, OfKind, Near, Place, Remove, CanPlace), Noise, a seeded Rnd and Print. A script runs in the background on a snapshot of the area, and what it changes goes in as one undo step, or nothing when it fails (its mistakes and errors give the line) or is stopped. There are examples to start from (a mountain range, terraces, a canyon with a river, scattered boulders, a ring of trees, a flat paved base, a noise landscape), and scripts are saved in the data folder's scripts folder. The program file is about 20 MB larger because of the C# compiler.

### Changed
- The editor's download no longer holds the WorldEditorBridge plugin. It is its own download, `WorldEditorBridge-<version>.zip`, on the same releases page and on Thunderstore and Hexium.

## v1.0.1 — 2026-10-08

### Fixed
- Linux: the 3D view and the map stayed blank without a GPU driver (Mesa's software OpenGL, llvmpipe) and in VMware virtual machines. This is fixed.
- Leaving a live world closes its connection to the game.

### Changed
- Internal code cleanup; no change for users.

## v1.0.0 — 2026-10-08 (the native app replaces the web editor)

### Added
- Native app: the game's look (terrain textures, map textures and models) is copied from your own Valheim on first start and again after a game update, as the web editor did. The start page shows the copy's progress, asks for the Valheim folder when it is not found, and offers Try again when it failed. Choosing another game folder in Settings checks it again.
- Native app: every control explains itself when you hover it, as in the web editor, and a row's label tells the same.
- Native app: a log (log.txt in the data folder, as the web app kept), the window's icon, the version in the title, and a Documentation link on the start page.
- Native app: a world folder (`ValheimWorldEditor <folder>`) or a live game (`--live <url> --token <token>`) given on the command line opens straight away, as with the web app.
- Native app, editor: Discard in the top bar undoes the steps not saved (or applied) yet and keeps the rest of the history. Changes without a step here are discarded by reading the world again.
- Native app, live: Reload in the editor's top bar, and the Auto switch that applies every change to the game at once (remembered).
- Native app, live: the players are shown in the 3D view with their names, and View has "Go to" a player.
- Native app, editor: move the area by one zone with ◀▲▼▶, or let it Follow the view near its edge (remembered). Follow waits while a selection, stroke, path or apply would be lost.
- Native app, editor: a note says how many locations are in or near the area.
- Native app, Select: saved selections. Keep the selection under a name, then select it again or add it later, also after saving the world (per world, found again by kind and place).
- Native app, Area and Select: "pick" next to Replace's list takes the kind from an object clicked in the world.
- Native app: the editor remembers its choices between runs, as the web editor did: the View switches, the map's place, the brush's shape and falloff, the Area action, the Shape preset and your own formula, Select's options, the open right-hand panel and the clipboard.
- Native app: the status bar shows where the mouse is (x/z), the ground's height against the original (and the ±8 m limit) and the zone.
- Native app, View: Game look on/off, See-through buildings, 3D resolution (Sharp, Balanced, Fast), and Defaults / All / Ground presets.
- Native app: `[` `]` change the brush size, H picks the View tool, Space + left drag and Shift + right drag slide the view in any tool, and Alt + wheel turns the selection, the paste or the Place preview. The help lists every key.
- Native app, Naturalize: New pattern (another noise pattern for the next stroke or natural path).
- Native app, Area: cut and fill follows every change of the ground and of the action.
- Native app, Place: **fit** sets the spacing when a chosen kind is wider than it (the panel says so).
- Native app, Place chooser: Shift + click on Pick from world adds the kind, and on a favourite or recent places only it. Also Untick all, all / none per row, and categories that fold, with which are open remembered.
- Native app: new objects not saved (or applied) yet carry a green dot in the view.
- Native app, View: **Unsaved marks** hides the green dots on new objects (remembered).
- Native app, View: **Limit marks** hides the red on ground at the game's ±8 m limit (remembered).
- Native app, Path: before a click, a circle under the cursor shows the path's width and soft edge, and the stretch the next point adds, and the ground the path covers is tinted.
- Native app, Select: the object under the cursor is outlined faintly before a click picks it.
- Native app, Place: the brush's outline shows under the cursor (Brush mode).
- Native app, View: **Tamed animals** (boars, wolves, lox, hens, deer, necks… that players tamed, with their models) and **Runestones**, both shown at first. Runestones can be picked and deleted, offline and live, but not moved or copied. The game's look is copied again once for their models.
- Native app: placing or pasting a kind switched off in View switches it on, and says so.

### Fixed
- Native app: after a visit to the map, the 3D editor drew its models without textures (trees as big white and blue leaf cards). This is fixed.
- Native app, Area: after choosing a backup folder with "Another folder…", the folder picker opened again and again, and a folder chosen by hand was also dropped for live worlds. Both are fixed.
- My game: on Linux, mod manager profiles were listed and searched twice.
- A character file that cannot be read no longer stops worlds from opening.
- Native app: in a short window the View panel and the tool panels ran under the status bar and their last rows could not be reached. They now stop above it and scroll.
- The data folder follows `XDG_DATA_HOME` on Linux even before that folder exists (it fell back to `~/.local/share`).
- Native app, start page: empty error lines and an empty server list left gaps, and the data folder is now shown as `~/…`.
- Native app: Space no longer presses the focused button in the editor.
- Native app, Place: Esc cancels Pick from world.
- Native app, Area: "pick" for Replace now works before an area is drawn.
- A character with a backup copy of its file (`ragnar_copy.fch`) could be listed under the copy's name, depending on the disk. The original is now kept.
- Native app: folders under the home are shown as `~/…` everywhere (saving, backups, blueprints, Settings), so no user name is on screen.
- Native app, Settings: the dialog is as tall as what it holds (it had a large empty part).
- Native app: the tools' lines (brush circles, path, area, measure, selection boxes) broke into faint dashes when the 3D view ran below the screen's resolution. They are now drawn a few pixels wide.

### Removed
- The web editor (the page in a WebView2/WebKitGTK window or the browser): the native app replaces it, with every feature it had. Its JavaScript Script console is not carried over (C# scripts may come later).
- Native app: the development command-line options (pictures, scripted tools); `--world`, `--zone` and `--size` remain.
- Internal code cleanup; no change for users.

## v0.41.1 — 2026-10-07

### Fixed
- Apply live: an error answered by the game (the plugin refusing a call) is now reported as "Could not apply live" instead of failing, and the changes stay waiting.
- Apply live: when the game refused the objects, deletions were already counted as sent and were never sent again. Now nothing counts as applied until the game accepts it.
- Apply live twice at once (a double click) could create the same new objects twice in the game.
- Native preview, Select: Drop (End) right after selecting an object above another one put it too low, by as much as it floated above the ground.
- Native preview, Place: a double-click on the last point of a line or zone did not place it.
- Native preview, map: the 1 m close-up never showed when zoomed in, and the coarse whole-world map was drawn instead.
- Native preview: after going between the map and the 3D editor, both could draw with errors or the wrong buffers. They are rebuilt now.
- Native preview, Place: a circle of walls end to end came out one wall short (a gap), the last one falling a hair past the end of the ring.
- An object whose data is edited or moved keeps the game's short position form.

## v0.41.0 — 2026-10-07 (plugin settings file renamed)

### Changed
- The editor finds the plugin's settings under either name (your game, and the server's Valheim folder over SSH), so it works with older plugins too. Every hint and guide names the new file.

### WorldEditorBridge
- The plugin's id is now `Tie.WorldEditorBridge`, so its settings are in `BepInEx/config/Tie.WorldEditorBridge.cfg` (was `local.worldeditorbridge.cfg`). On its first start the plugin moves the old file to the new name: the port and token are kept, and saved servers in the editor keep connecting.

## v0.40.1 — 2026-10-07

### Changed
- The frame rate log is written only on demand: **Record frame rates** in Help, off each time the editor opens.

## v0.40.0 — 2026-10-07 (3D resolution)

### Added
- **3D resolution** (View, Look): Sharp (the screen's own pixels), Balanced (one per screen point: a quarter of the pixels on a screen scaled ×2) or Fast. Fewer pixels give more frames per second, and the frame rate logs say which one was used.

## v0.39.0 — 2026-10-07 (frame rate log)

### Added
- **Frame rate log**: a line every 0.2 s while the 3D view is used (camera moving, or still while something else happens), with frames per second, the editor's work per frame and the longest gap between frames. It goes in `perf-window.log` (the app's window) or `perf-browser.log` (`--browser`) in the data folder, to compare the two. Header lines give the browser, the view's size and what is loaded.

## v0.38.3 — 2026-10-07

### Fixed
- The frame rate line in Help could not be selected to copy it, because it was rewritten several times a second. It now stays as it is while selected.

## v0.38.2 — 2026-10-07 (frame rate in Help)

### Added
- **Help** (`?`) shows the frame rate while the view moves, and how many milliseconds of work each frame takes, to tell a slow graphics path from a busy editor.

### Fixed
- Help said "the graphics card" for the app's own window on Linux and Windows, although it cannot tell what draws the view. The window now says it does not tell.

## v0.38.1 — 2026-10-07 (fixes found by new tests)

### Fixed
- Area tool: after Undo or Redo, the counts of what is inside the selection and the Replace list still showed what was there before (the Replace list could be empty while trees were back).

## v0.38.0 — 2026-10-07 (lighter 3D view)

### Changed
- The 3D view is drawn only while something happens (the view moves, a stroke, the mouse or keys in use) and a few times a second otherwise (enough for the game look's water and sky). An idle editor leaves the processor alone instead of drawing 60 times a second.

### Added
- **Help** (`?`) says what draws the 3D view: the graphics card, or the processor (llvmpipe, SwiftShader), which is much slower. The editor says so once when it is the processor.

## v0.37.0 — 2026-10-07 (pieces that snap, elevation)

### Added
- **Snap to pieces already there** (Place tool, building pieces): a wall, fence or floor locks onto the pieces around it like the hammer, **Beside** (end to end, at their level) or **On top** of the piece under the cursor, facing like it (Rotation turns it in quarter turns). Pieces are placed one at a time under the cursor.
- **Elevation** for everything placed: on the ground, a height **above the ground**, or **at one height** for all. `PgUp` / `PgDn` change it, and `Alt` + click takes the top of a piece or the ground.
- **Layers** (Line, End to end): a line of walls or fences stacked several pieces high.
- A line started next to a piece already there continues it, at its level.

### Fixed
- Walls and other pieces with their origin in the middle were half sunk into the ground when placed one by one. They now stand on their bottom.

## v0.36.0 — 2026-10-07 (tidier tool panels)

### Changed
- **Tool panels** show the settings you use most, and fold the rest under one line at the bottom that says what is inside (shape, falloff, stamp, curve, clumping, tilt, facing...), open or closed as you left it. The **Mask** moves into that fold, which says "mask on" while it is on.
- **?** next to the tool name shows what the tool does and how to use it. The how-to text is hidden otherwise (in every tool, until you click ? again).
- **Area**: one **Action** list (Ground, Objects inside, Copy, Bring back) instead of six sections. The panel shows only that action's settings, and one button at the bottom does it (`Enter` too).
- **Place**: the panel lists the chosen kinds with their weights and a ✕ each, and **+ Add kinds** opens the list of every kind (search, favourites, recent, Pick from world) in a drawer beside the panel.
- **Select**: Delete, Whole building, Same kind, Invert and Exact place first, with Replace with, saved selections, Inspect data and Make player built under **More actions** and the moving switches under **Moving**.
- **Path**: Soft edge, curve and natural look are folded, and Apply and Clear stay at the bottom.

## v0.35.0 — 2026-10-07 (script console)

### Added
- **Script console** (Script in the top bar, like MCEdit's filters): a few lines of JavaScript with a small API (find, add and remove objects, read and set the ground, work inside the Area selection) for jobs the tools do not cover. Each run is one undo step. It comes with examples (count kinds, remove young beeches, rocks on steep slopes, terraces), and your own scripts are kept in this browser. See docs/script.md.

## v0.34.0 — 2026-10-07 (cut and fill)

### Added
- **Cut and fill** in the Area tool shows, before you click, how much ground inside the selection has been raised and dug since it was generated, and what Flatten, Raise or Lower would dig and fill (m³), with what the ±8 m limit leaves out.

## v0.33.0 — 2026-10-07 (overlays)

### Added
- **Ward areas, Build ranges and Location flattening.** New rings under View, Overlays show where wards protect, where crafting stations let you build (10 to 40 m depending on the kind), and where locations flatten the ground.
- **Go to a player.** In live mode, View can jump right to a player, or move the work area to them.

### Fixed
- After Undo or Redo in the Place tool, spacing still counted the objects as they were before. It now uses the current ones.

## v0.32.0 — 2026-10-07 (walk and fly)

### Added
- **Walk and Fly views (`F`).** See the world through a player's eyes, 1.8 m above the ground (or the water), and walk with WASD (Shift runs). Press `F` again to fly (Space up, C down), and once more to return to the usual view. Tools keep working, and Follow and the Area arrows keep the view.

## v0.31.0 — 2026-10-07 (area that follows the view)

### Added
- **Follow.** Next to the Area arrows, it moves the area by itself when the point you look at comes within 12 m of its edge. It waits, and tells you why, while a stroke, a path or Place shape, an Area selection or selected objects would be lost.

### Changed
- Moving the area (arrows or Follow) now keeps the view on the same spot and keeps your tool, instead of starting over above the middle with the Raise tool.

## v0.30.0 — 2026-10-07 (history survives reloads)

### Changed
- **History now survives a page reload and a move of the work area.** The editor keeps it with the pending changes. A change that touched ground outside the new area is left out, along with the ones before it. Saving, Discard and reloading from the game still start a new history.

### Fixed
- A long status bar message wrapped onto several lines and covered the last tool panel buttons so they could not be clicked. It now keeps to two lines, with the whole text on hover.
- "Switched on … in View" replaced what the tool had just said (how many objects were placed or regrown). It is now added to it.
- Regrow nature offered kinds the page cannot place, and then reported them as regrown. It no longer does.

## v0.29.0 — 2026-10-07 (regrow nature)

### Added
- **Regrow nature (Area tool).** Puts back the game's own vegetation inside the selection, for the kinds chosen with the chips: the trees, rocks, bushes and pickables the game grows there (how many, on which slopes, heights and biome edges, in groves), worked out like the game does, on the ground as it is now. Spots where something already stands are skipped, and so is the ground near buildings. On a world the game generated, most of what it places lands exactly where the game put it.

## v0.28.0 — 2026-10-07 (clumping)

### Added
- **Clumping and Patch size (Place tool, Brush and Zone).** Objects gather in groves with clearings between them, following a pattern fixed to the world, instead of an even spread. Presets can keep them (Meadows woods, Black forest, Berry patch and Meadows rocks use some).

## v0.27.0 — 2026-10-07 (weighted mixes and presets)

### Added
- **Weights (Place tool).** With several kinds ticked, a slider per kind sets how often it is used (Beech 3, Birch 1 gives three beeches for one birch), with its share in %.
- **Presets.** Load a mix (kinds, weights, Density, Spacing, Size, Tilt, facing) from built-in looks (Meadows woods, Black forest, Swamp, Berry patch, Forest floor, Meadows rocks), or save your own.

## v0.26.3 — 2026-10-07 (spacing that fits)

### Added
- **Spacing that fits (Place tool).** When a ticked kind is wider than the spacing, the panel says so, and **fit** sets the spacing to it. A blueberry bush is about 3 m wide, so a 1 m grid of them used to be one big hedge instead of rows.

## v0.26.2 — 2026-10-07 (editable zone points)

### Added
- **Editable Zone points (Place tool).** Drag a point to move it, drag the outline to add a point, Ctrl + click a point to remove it, as with lines. The hints above Place now explain how, for lines too.
- **Remove last point** button (Zone and Line), the same as Backspace.

### Changed
- **Leave saplings and crops room to grow** now also applies to grown crops (Pickable_Carrot...) and to trees that grow from saplings (Beech1, Oak1...), which keep the room their sapling needs (as the game defines it).
- **Zone** now fills at random only: its Scatter / Grid choice is gone, since Grid mode makes grids.
- **Grid** now uses as many whole cells as fit best in the box you drag (at least one), centred. A box a little short of N cells used to lose a row, and a small box got nothing. Its **Cell** slider is now called **Spacing** (space between objects).

## v0.26.1 — 2026-10-07 (placed objects follow View, chip toggles, Nobody builds)

### Changed
- Objects you placed now follow the View switches like the others (before, they stayed shown until saved). Switching off Trees & logs hides the trees you just planted too.
- Clicking a **Favourite** or **Recent** kind now ticks or unticks it (before, it placed only that kind, which Shift + click now does), and each row has **all** / **none**.

### Added
- **Untick all** in the Place tool, next to Pick.
- **Nobody (not player built)** in Built by, to place pieces without a builder.

## v0.26.0 — 2026-10-07 (Place tool)

### Changed
- **The Plant tool is now the Place tool** (still `T`). It places any kind of object: walls and fences end to end along lines, circles and rectangles, as well as trees and rocks with a brush. Its guide is now docs/place.md.
- **Leave saplings and crops room to grow** now only shows, and only applies, when a sapling or crop is ticked.

## v0.25.0 — 2026-10-07 (player-built pieces)

### Added
- **Built by (View panel, Building).** Sets the player written as the builder of the pieces you place. It lists the world's builders (the one with the most pieces is chosen at the start), the players named on beds, wards and tombstones, and the characters on this computer, and you can type any other player id. It is remembered per world.
- **Make player built (Select tool).** Gives the chosen builder to selected pieces that have none.

### Fixed
- Pieces placed by the editor were not always **player built**. Kinds the world had none of, and copies of pieces from ruins, had no builder, so the game took them for ruins (a third of the materials back, no base for fires, ignored by raids, wards and private chests owned by nobody). Now every piece of a kind the hammer, hoe, cultivator or serving tray can place gets the chosen builder when it is placed, pasted or built, while moved and edited objects keep their builder.

## v0.24.0 — 2026-10-07 (eyedropper and favourite kinds)

### Added
- **Eyedropper.** The Plant tool's **Pick** button and the **pick** buttons next to the Replace lists (Select tool, Area tool) take the kind of the object you click in the world.
- **Favourites and Recent kinds (Plant tool).** Star kinds with ☆ in the list, and the last eight kinds you placed come back as buttons above the list. A click plants only that kind, and Shift + click adds it to the ticked ones.

### Changed
- The Select tool's Replace row now reads "Replace with", and its button is now "Replace the selection".

## v0.23.0 — 2026-10-07 (sharp edges)

### Added
- **Sharp edge brush falloff.** Full strength right to the brush's edge and nothing beyond, for steep walls like the game's pickaxe.

### Changed
- With **Soft edge 0**, Path and Area actions now give the ground points their edge crosses a share of the change, so a diagonal edge follows the line straight instead of stepping from point to point.

## v0.22.0 — 2026-10-07 (fine-tune drawn lines)

### Added
- **Fine-tune drawn lines (Path tool, and Plant lines).** Drag a point to move it, drag the line to add a point there, Ctrl + click a point to remove it, and the preview follows.

## v0.21.2 — 2026-10-07 (buildings keep their shape)

### Changed
- **Moving a building "on the ground" keeps its shape.** Its pieces move as one block, which goes up or down until its bottom layer sits on the ground (sinking into a slope rather than floating). Trees, rocks and other objects in the selection still land on the ground one by one. Before, every piece was put on the ground by itself, pulling buildings apart.

## v0.21.1 — 2026-10-07 (ring brush outline)

### Changed
- The Ring brush shape now draws its inner edge too, so the untouched middle shows on the ground.

## v0.21.0 — 2026-10-07 (exact moves and snapping)

### Added
- **Snap to other pieces (Select tool, on by default).** While moving, a piece's snap point locks onto the nearest snap point of a piece around it, so walls, floors and fences join like with the hammer.
- **Turning ring** around the selection: drag it to turn, Ctrl for 15° steps.
- **Exact place.** Type the selection's position and turn, or how far to move and turn it.

### Fixed
- "Put each object on the ground" put the middle of pieces whose origin is in the middle (walls) on the ground, sinking them. Their bottom now rests on the ground.

## v0.20.5 — 2026-10-07 (pieces follow the line)

### Changed
- **End to end now switches itself on** in the Plant tool's Line mode when every ticked kind is a piece the game snaps (fences, walls, stakes...), and off for other kinds, until you change it by hand.
- **Objects follow the line.** Circles and rectangles always turn their objects along the outline, and "Follow the line" (before: Face along the line) is on by default for drawn lines. A piece now lies along the line (its length, not its front, follows it).

## v0.20.4 — 2026-10-06 (dropdown boxes)

### Fixed
- Closed dropdown boxes were drawn by the system theme (a light box in the app window on Linux) under the editor's light text, so the chosen value could not be read. The editor now draws them itself, dark, with its own arrow.
- A dropdown followed by a button (Replace … go, Paint … apply, Saved … keep) pushed the button onto the next line.

## v0.20.3 — 2026-10-06 (readable dropdowns)

### Fixed
- Dropdown lists could be unreadable (pale text on a pale background) depending on the system theme. They are now always drawn dark, with the chosen entry highlighted in the editor's amber.

## v0.20.2 — 2026-10-06 (dropdown labels)

### Fixed
- In the app window on Linux, the group headings of the Replace lists ("Trees & logs"...) could show blank, with GTK warnings in the terminal. They now read "Trees and logs" and so on.

## v0.20.1 — 2026-10-06 (fences on slopes)

### Changed
- **End to end on slopes.** Every piece now stays level and stands on the ground where it is, a little higher or lower than the last one and touching it, like a fence going up a hill. Before, each one sat on the lower of its two ends, which could leave it floating at the middle of a bump.

### Fixed
- End to end placed nothing at all when one of the ticked kinds had no known length yet. Such kinds are now left out and the others are placed.

## v0.20.0 — 2026-10-06 (fences end to end)

### Added
- **End to end (Plant tool, Line mode).** Pieces are placed so that each one starts where the last one ends, turned along the line, like the game's hammer snaps them, using the pieces' own snap points or the model's length for other kinds.
- **Circle and Rectangle lines (Plant tool).** Press at the centre or a corner and drag to the size, and use **Close the loop** for drawn lines. With End to end the sizes snap to whole pieces, so a fenced ring or yard closes exactly.

## v0.19.2 — 2026-10-06 (moved objects on the ground)

### Changed
- **Moved objects land on the ground (Select tool).** Each moved or turned object now stands on the ground where it ends up. A new switch, "Put each object on the ground when moving" (on by default), turns this off to keep each object's height above the ground, as before (useful for buildings).

## v0.19.1 — 2026-10-06 (area selection)

### Added
- **Clear** in the Area tool, next to Box and Polygon, to start a new selection (as `Esc` does).

### Fixed
- The Area tool's selection outline stayed on screen in every other tool. It is now only drawn in the Area and Paste tools, and the selection is kept for when you come back.

## v0.19.0 — 2026-10-06 (selection helpers)

### Added
- **Same kind, Invert and saved selections (Select tool).** Select every object of the selected kinds, select the opposite of the selection, or keep a selection by name for the world.

## v0.18.0 — 2026-10-06 (select a whole building)

### Added
- **Select a whole building (Select tool, like Axiom's magic select).** Double-click a piece, or press Whole building, to select every piece connected to it.

## v0.17.0 — 2026-10-06 (river and canal)

### Added
- **River / canal (Path tool).** Digs a U-shaped bed below sea level with sloped banks, so the sea flows in.

## v0.16.0 — 2026-10-06 (shape tool)

### Added
- **Shape (tool `G`, like WorldEdit's `//generate`).** One click puts a mound, cone, mesa, crater, moat, bowl or ridged hill into the ground, or any shape written as a formula.

## v0.15.0 — 2026-10-06 (erosion)

### Added
- **Erode (sculpt tool `O`, and an Area action).** Thermal erosion settles slopes to a rest angle, and water erosion cuts gullies and fills hollows.

## v0.14.0 — 2026-10-06 (heightmaps)

### Added
- **Heightmaps (Area tool, like WorldPainter).** Export the area's ground as a 16-bit grayscale PNG, and import a picture back into the selection or the whole area, between chosen heights.

## v0.13.0 — 2026-10-06 (stamps)

### Added
- **Stamps.** Pictures work as brush shapes (built in: mountain, mesa, crater rim, dunes, rocky ground, or any picture loaded from a file), and "Stamp once" puts a whole stamp into the ground with one click, to a chosen height.

## v0.12.0 — 2026-10-06 (brush shapes)

### Added
- **Brush shapes and falloff (sculpt and paint tools, like WorldPainter).** Circle, square, ring or ragged brushes, with a smooth, linear, dome, flat-top or peak falloff. Square brushes turn.

## v0.11.0 — 2026-10-06 (restore from a backup)

### Added
- **Restore from a backup (Area tool, like WorldEdit's `//restore`).** Brings back the ground and objects of a selection as they were in one of the world's backups (the editor's or the game's), with all the objects' data.

## v0.10.0 — 2026-10-06 (reset zones across the world)

### Added
- **Reset zones across the world (map page, like MCA Selector).** Pick zones by biome, distance from buildings, ground edits and distance from the centre, and mark them all for the game to generate again.
- **Zone resets in live mode (needs WorldEditorBridge 0.10.0 or newer).** Apply live resets the marked zones in the running game, which generates them again at once where players are.

### Fixed
- A zone reset also removed players' tombstones, with what they carried. They are now always kept.
- Saving twice within the same second failed, because both backups got the same folder name.

### WorldEditorBridge
- New: the editor can now reset zones in the running game (they are generated again at once where players are). Players' tombstones are always kept.

## v0.9.0 — 2026-10-06 (search the world)

### Added
- **Search the world (map page).** Find every object of a kind, every container holding an item, or every sign, portal or ward whose text matches. Results are counted and pinned on the map, and a result opens the 3D editor with that object selected.

## v0.8.0 — 2026-10-06 (object inspector)

### Added
- **Object inspector (Select tool, `I`).** Shows everything an object holds in the save, by name, and lets you edit it: chest contents (items, stacks, quality, slots), sign texts, portal tags, ward permissions, timers... An edit is one undo step and is saved or applied live like any change.

### Fixed
- Objects placed or moved in a session lost what they were copied from after the page was reloaded, so moving one again made a fresh copy (a moved chest lost its contents).

## v0.7.0 — 2026-10-06 (repeat a paste)

### Added
- **Repeat a paste (like WorldEdit's `//stack`).** One click places several copies in a row, along the copy's width or depth or stacked upwards, with a gap, as one undo step.

## v0.6.0 — 2026-10-06 (PlanBuild files)

### Added
- **PlanBuild and .vbuild files.** Import a PlanBuild `.blueprint` (its terrain marks become ground) or a `.vbuild` file into the blueprint list, and write any blueprint back out in either format.

## v0.5.0 — 2026-10-06 (blueprints)

### Added
- **Blueprints (Area tool → Copy & paste).** Save the clipboard as a named file, browse the saved ones with a picture of each, and paste them into any world. They are kept in the `blueprints` folder of the editor's data folder.

## v0.4.1 — 2026-10-05 (editor on GitHub only)

### Changed
- **Thunderstore now carries only the plugin** (`Tie-WorldEditorBridge`). Thunderstore does not host programs and rejected the `ValheimWorldEditor_Windows` and `_Linux` packages, so the editor is downloaded from the GitHub releases page, which the plugin's mod page now points to.

## v0.4.0 — 2026-10-05 (clean-up)

### Fixed
- Leaving the world map with **Worlds** while it was still loading could raise an error on the page.
- Internal code cleanup; no change for users.

## v0.3.3 — 2026-10-05 (Thunderstore dependency)

### Changed
- **Thunderstore editor packages no longer carry their own copy of the plugin.** They depend on `Tie-WorldEditorBridge`, which mod managers install with them (one copy per profile, and plugin fixes no longer need a new editor package). The GitHub downloads keep it in `plugin/`.
- The start page's plugin steps now point mod manager users to WorldEditorBridge on Thunderstore.

## v0.3.2 — 2026-10-05 (smaller packages)

### Changed
- **Smaller downloads.** The bundled Python keeps just the files a full game-look export uses (the export's output is the same): 117 → 87 MB on Linux, 74 → 48 MB on Windows. Also gone: `requirements.txt`, the Linux copy of `icon.ico`, and in the Thunderstore packages the `README.txt` files (the mod page replaces them).

### Fixed
- **Windows:** the bundled Python now carries `msvcp140.dll`, which is needed to copy the game's look. A Windows without the Visual C++ runtime could not do it before.

## v0.3.1 — 2026-10-05 (Thunderstore packages)

### Added
- **The editor on Thunderstore.** `ValheimWorldEditor_Windows` and `ValheimWorldEditor_Linux` hold the editor with the plugin, for r2modman and Thunderstore Mod Manager, while `WorldEditorBridge` stays as the plugin alone, for servers.
- Installed by a mod manager, the editor finds its own profile for **My game**.

### Fixed
- Linux: the game-look export now runs even when mod managers unpack the bundled Python without its run permission. The release no longer holds the `python`/`python3` links.

## v0.3.0 — 2026-10-05 (Thunderstore)

### Added
- **WorldEditorBridge on Thunderstore.** Install the live-editing plugin with r2modman or Thunderstore Mod Manager, and BepInEx comes with it.

### Changed
- **One version number** for the editor and the plugin (0.3.0, the plugin's version until now). The plugin file's own version said 0.1.0 and now says 0.3.0 too.

### WorldEditorBridge
First release on Thunderstore; from here on the plugin has the editor's version number: use the two
together.
- **Objects live**: deleted, planted, pasted and moved objects are applied in the running world, and undoing after applying takes them back in the game too.
- **Ground live**: terrain edits (sculpt, paint, roads, areas) are sent to the game and shown to every player at once.
- **World snapshot**: the editor opens the running world as the game has it now, without a save.
- Listens on `127.0.0.1:5182` only, with a random token in `BepInEx/config/local.worldeditorbridge.cfg`.

## v0.2.0 — 2026-10-05 (desktop app)

Download, unpack, double-click: no command line, no Python, nothing to install.

### Added
- **Its own window** on Windows and Linux (the system's web engine, with the browser as fallback or with `--browser`), with an icon.
- **Start page with three ways to edit**, each clearly separate:
  - **My game (live)**: finds Valheim running with the plugin (also in r2modman / Thunderstore Mod Manager profiles) and reads its token from your own game's settings. One click to edit live, or it shows the steps that are missing (BepInEx, the plugin, starting the game).
  - **A dedicated server (live)**: the editor logs in over SSH (password or key, no `ssh` program needed) and makes its own tunnel to the plugin. The plugin token is always typed by you. Servers are saved in `servers.cfg` after the first connection for one-click access, with the password only when "Save password" is ticked. The server's SSH identity is remembered and a change is refused.
  - **A saved world (offline)**: the worlds found on the computer (Valheim's usual folders, Proton included), recently opened ones, and any other folder (with a folder dialog).
  **Worlds** on the map page goes back to the start page.
- **Settings** for folders that are not in the usual place: the Valheim game folder, extra BepInEx folders or mod manager profiles, and world folders that are always listed. For a server, an optional **Server's Valheim folder** lets the editor read the plugin's port from it, and when the plugin does not answer the editor says exactly what is missing there.
- **Automatic game look.** The editor finds Valheim in the Steam libraries and copies the game's textures and models from it by itself, with progress on the page. It asks for the folder when Valheim is not found, and copies again after a Valheim update. The copy lives in the per-user data folder, next to the settings and a log.
- **One download per system**, each with the plugin in `plugin/` and instructions for that system.

### Changed
- The program is now called `ValheimWorldEditor`. It picks the first free port from 5180, so a second copy or another program never blocks it.
- The game-look exporter comes with its own small Python runtime, and its texture fixes need only Pillow now (same visible result).

### Fixed
- Connecting to a server that does not answer gave up only after 3 minutes. The editor now checks the bridge first (at most 10 seconds) and says what is wrong: no tunnel, wrong token, or a game that does not host the world.
- The app now quits cleanly when the system asks it to (logout, shutdown), closing its window.

## v0.1.0 — 2026-10-05 (first release)

Everything below, packaged: the editor for Linux and Windows (x64), and the WorldEditorBridge
server plugin (0.3.0) for live mode. Game files are not included.

### Added
- **Release packages.** The program, web page and export tool, without source code or game files.
- **One-command game export.** A single command copies the game's terrain shader and textures, map textures and the models of every kind of object from your own Valheim install (a few minutes). It replaces the separate scripts that had paths of the development machine written in.

### Changed
- Models are now exported for every placeable kind (about 800 world objects plus 689 building pieces), not only the kinds one world happened to contain, so the Plant tool shows real models for far more kinds.

## 2026-10-05 — Placing, selecting and moving

### Added
- **Hover help on every control.** Each slider, switch, button and list in the editor and on the map explains exactly what it does, and tool buttons also show what their tool does.
- **Documentation.** A README with installation for offline and live mode, a page per feature in `docs/` with screenshots, and this changelog.
- **Select inside a zone.** In the Select tool, drag on empty ground (or Alt + drag anywhere, for forests) to draw a zone, and everything shown inside it is selected. Shift adds to the selection.
- **Place any game object.** The editor now knows every network object in the game (about 1,500 placeable kinds), so you can plant or place things the world has none of yet, such as turnips. Such objects are created exactly like the game creates a freshly placed one.
- **Leave saplings room to grow (Plant tool, on by default).** Saplings and crops are never placed closer than their in-game grow radius to anything, and the panel also reminds you when a crop needs cultivated ground.
- **Drop to surface (Select tool, `End`).** Drops the selection onto the object below it, or onto the ground when there is none.
- **Move arrows (Select tool).** Red X, green Y and blue Z arrows at the selection: drag one to move along that axis only, and Ctrl snaps to 0.5 m.
- **Plant: Line, Grid and Zone modes.** Place objects every N metres along a drawn line (optionally smoothed, facing along it, with sideways wiggle), one per cell of a dragged box, or fill a freely drawn zone by scatter or by an orderly grid. Zones and grids can be turned as a whole.
- **Foldable View panel.** Click a section title (Look, Nature, Spoilers, Overlays) to fold it. Look starts folded and your choice is remembered.

### Changed
- **Rotation in 1° steps everywhere** (`,` `.` and Alt+wheel), 15° with Shift, in Select, Plant brush, Plant zones/grids and Paste. Paste can now be turned to any angle, not only quarter turns.
- **Flatten now levels to the height where the stroke starts**, instead of a fixed 35 m that made it dig like Lower on higher ground. A fixed height is still available (untick the option, or Alt + click).
- **The Mask judges the ground as it was when the stroke started**, so raising past the mask's height limit or changing the slope no longer stops a stroke half-way. Settings that let nothing through (like a 0° maximum slope) are flagged, and an empty stroke says the Mask left everything out.
- Placing a kind whose View switch is off now switches it on, so what you placed stays visible after a reload.
- The move arrows are smaller and leave the object's middle free for a free drag.
- The editor now tells the browser to revalidate its page files on every load, so an update is used right away instead of an older cached copy.

### Fixed
- With Game look off (or without the extracted game files) the world's objects were not loaded, so nothing could be planted, placed or saved as an object. They now always load.
- The world map no longer fails to load when the map textures are missing. It uses plain colours.
- Pressing a second key (End, PgUp, turn) right after a move acted on the hidden original instead of the moved copy.

## 2026-10-05 — History, discard and live objects

### Added
- **History panel (`L`).** Lists every change with its time. "Back to here" rolls back to any point, and "Remove" takes out a single change while keeping everything done after it.
- **Move, turn, lift, copy and paste selected objects** in the Select tool (drag, `,` `.`, PgUp/PgDn, Ctrl+C / Ctrl+V).
- **Plant preview.** See-through "ghosts" show exactly what a click will place, for every kind. `R` rolls a new layout, Alt+wheel turns it, there is a single placement mode, and categories fold.
- **Objects live (WorldEditorBridge 0.3.0).** Deleted, planted, pasted and replaced objects are applied to the running game, and undo works after applying.
- **Ground live (WorldEditorBridge 0.2.0).** "Apply live" and "Auto" send ground edits to the game.
- **Live mode (WorldEditorBridge plugin).** The editor can open the running world of a server, with its players, instead of a save on disk.

### Changed
- Copies, plants and pastes create **fresh, independent objects**: a copied chest is an empty chest. Moving an object keeps all of its data.
- **Discard** undoes only the changes that are not saved or applied yet, in place, without reloading the page (tool, view and zoom are kept).
- The pending counter only counts zones that really differ from their saved state, so undoing back to the start clears it.
- Objects you placed but have not saved yet are always drawn (with a green marker), whatever the View switches say.

### Fixed
- Raspberry bush leaves disappearing in the plant preview and with see-through buildings.
- The brush ring and other moving overlays disappearing when zoomed in close.
- A few kinds (Bush01, shrub_2 and others) drawn as boxes because the wrong model was matched.

## 2026-10-05 — World-editor tools

### Added
- **Masks** for brushes, paths, area actions and planting: by biome, height range, slope range and paint.
- **Area tool (`B`).** Box or polygon selection. Flatten, raise, lower, smooth, naturalize, restore or paint the ground inside; remove, select or replace objects inside; copy and paste ground and objects (turn and mirror); reset zones so the game generates them again.
- **Plant tool (`T`).** Paint trees, rocks, bushes and more onto the ground with density and spacing. Shift + drag removes the chosen kinds.
- **Measure tool (`M`).** Distance, height difference and slope between two points, plus slope colours and height lines in View.
- **Delete objects** (Select tool, `Del`), and a reworked interface: top bar, tool rail, tool panel and View panel.

## 2026-10-05 — In-game look

### Added
- The 3D editor draws the world like the game: the game's own terrain shader and textures, water and sky, real models for buildings, trees, rocks and bushes, and show/hide switches per kind (spoilers such as ore and ruins are off by default).

## 2026-10-04 — First versions

### Added
- **World map** in the browser, drawn with the game's own map shader, with player buildings, painted ground and edited zones.
- **Base terrain from the seed.** A port of Valheim's world generator that matches the game bit for bit.
- **3D terrain editor** with brushes (Raise, Lower, Flatten, Smooth, Naturalize, Restore), ground paint (Dirt, Cultivate, Paved, Clear), the Path tool, undo/redo, and the game's ±8 m limit.
- **Saving back to the world.** A new save number with a full backup and a read-back check.
- **CrossplayJoinCodeFix** (separate plugin, own repository): keeps a crossplay server from appearing offline when PlayFab answers the join-code check incompletely.

