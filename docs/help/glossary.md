# Glossary

The words the editor and this help use, A to Z. Each entry links to where it is explained.

**Add-on.** A separate download that teaches the editor about a mod (Expand World, Riverheim, Tunnelheim), unpacked
into the `addons` folder next to the editor. See [Install the editor](../start/install.md#add-ons-for-mods).

**Alt biome.** A variant of a biome the game gives some areas from the seed (Dark Meadows, Rock Black Forest…), with
what grows there. The map's readout names it. See [Area, copy and paste](../area.md) (Regrow nature).

**Anchor.** The middle of the Workshop's plot, at ground level: the point a Homestead blueprint measures every piece
from. See [The Workshop](../workshop.md).

**Applied.** Sent to the running game with **Apply live** (or **Auto**). History tags such steps **applied**; they
can still be undone. See [Applying live](../concepts/saving-and-history.md#applying-live).

**Area.** The block of zones open in the 3D window, from 3 × 3 to 17 × 17 zones. See
[The area](../concepts/areas-and-zones.md#the-area).

**Area tool.** The tool (`B`) that selects a box or polygon of ground to level, clear, copy, restore or reset. Not the
same as the area above. See [Area, copy and paste](../area.md).

**Auto.** A live-mode switch that applies every change to the game as soon as you make it. See
[Live mode](../live-mode.md).

**Backup.** A copy of a world's folder, named `<World>_backup_<something>` next to it. The game makes its own
(`_backup_auto-`); you make yours before editing. See [Backups](../concepts/saving-and-history.md#backups) and
[Back up a world before editing](../guides/back-up-a-world.md).

**BepInEx.** The mod loader for Valheim. The game or server that hosts a world needs it, with the WorldEditorBridge
plugin, for live editing. See [Install the editor](../start/install.md#the-plugin-for-live-editing).

**Biome.** A kind of land in Valheim: Meadows, Black Forest, Swamp, Mountain, Plains, Mistlands, Ashlands, Deep North,
Ocean. The [Mask](../masks.md) can keep a tool to one.

**Blueprint.** A saved building, in the format of the Homestead mod: built in the Workshop or copied from a world,
pasted into any world. See [The Workshop](../workshop.md).

**Bridge.** Short for WorldEditorBridge, the editor's plugin in the game. See **Plugin**.

**Brush.** A tool you drag over the ground: the sculpt brushes (Raise, Lower, Flatten…) and the paint brushes (Dirt,
Cultivate, Paved, Clear). Place has a brush too. See [Sculpt and paint](../terrain.md).

**Built by.** The player written as the builder of the pieces you place, chosen in the View panel. See
[Player-built pieces](../concepts/objects.md#player-built-pieces).

**Clipboard.** What the last copy or cut holds, kept until you copy again, even after the editor restarts. See
[Area, copy and paste](../area.md).

**Discard.** The top bar's button that undoes everything not saved or applied yet. See
[Undo a step, take back one change, or discard everything](../guides/undo-changes.md).

**Dungeon.** Rooms the game keeps 5000 m above their entrance on the ground: crypts, Frost Caves and the like.
See [How the game keeps dungeons](../concepts/dungeons.md#how-the-game-keeps-dungeons).

**Earlier session.** A History step kept from an earlier run of the editor, back when you open the world again. See
[History](../concepts/saving-and-history.md#history).

**Extension.** Code a mod brings to teach the editor about its pieces and data, switched on in **Settings**. See
[For mod authors](../modding.md).

**Falloff.** How a brush's strength fades from its middle to its edge. See [Sculpt and paint](../terrain.md).

**Follow.** A top bar switch: the area grows by itself when you look near its edge. See
[Follow](../concepts/areas-and-zones.md#follow).

**Game look.** Drawing the world with the game's own textures and models, read from your Valheim. Off: plain colours,
faster. See [View panel](../editor-basics.md#view-panel).

**Generated zone.** A zone the game has already made, because a player came near it. Only generated zones have data
the editor can save into. See [Zones](../concepts/areas-and-zones.md#zones).

**Ground disc.** An invisible location the editor writes to hold ground past the game's 8 m limit. See
[No limit](../concepts/ground-limits.md#no-limit).

**History.** The list of every change of the session, to undo, redo or remove one. See
[History](../concepts/saving-and-history.md#history).

**Homestead.** A Valheim building mod whose blueprints the editor saves; with it in your game you build them with
the hammer. Not needed to paste blueprints in the editor. See [The Workshop](../workshop.md).

**Inspector.** The panel that shows and changes what an object holds: a chest's items, a sign's text, a portal's tag.
See [Select](../select.md).

**Limit marks.** Red on ground that reached the game's 8 m limit. See
[Limit marks](../concepts/ground-limits.md#limit-marks).

**Linked group.** Objects a mod ties together, like a ship and its parts; selected and moved as one. See
[Select](../select.md).

**Live mode.** Editing a world while the game or server runs it, through the plugin. See
[How it connects](../concepts/live-editing.md#how-it-connects).

**Location.** A place the game puts into the world when it makes it: villages, the trader, boss altars, dungeon
entrances. Locations level the ground around them. See
[Locations level the ground](../concepts/ground-limits.md#locations-level-the-ground).

**Locked edge.** The outer line of ground points of the area, which the tools do not move, so the area joins its
neighbours. See [The locked edge](../concepts/areas-and-zones.md#the-locked-edge).

**Mask.** A card that keeps a tool to ground of a biome, height, slope or paint. See [Mask](../masks.md).

**MCP.** The way Claude Code and Claude Desktop call the editor's tools when you allow Claude to connect. See
[Claude](../claude.md).

**No limit.** A brush setting that moves ground past the game's 8 m limit, saved as ground discs. See
[No limit](../concepts/ground-limits.md#no-limit).

**Pending.** A change made in the editor and not saved or applied yet. The top bar counts them. See
[Pending changes](../concepts/saving-and-history.md#pending-changes).

**Player built.** A piece that stores who built it, as the game does for what the hammer places. See
[Player-built pieces](../concepts/objects.md#player-built-pieces).

**Plugin.** WorldEditorBridge, the editor's BepInEx plugin in the game or server that hosts the world. It lets the
editor read and change the running world. See [Install the editor](../start/install.md#the-plugin-for-live-editing).

**Prefab.** The game's internal name for a kind of object, like `Beech1` or `woodwall`. Searches accept it.

**Reload.** In live mode, reading the world again from the game. See [Live mode](../live-mode.md).

**Reset zone.** A zone marked so the game makes it again from scratch the next time it loads it. See
[Generate zones again](../guides/reset-zones.md).

**Save (numbered).** The game keeps a world as numbered saves (`_main.<n>.*`); **Save to world** writes the next one
and checks it. See [Saving a world](../concepts/saving-and-history.md#saving-a-world).

**Saved world.** A world's folder on this computer, edited with the game closed. See
[The editor window](../editor-basics.md#the-start-page).

**`savedir`.** The folder a dedicated server keeps its worlds in (its `-savedir` option); its worlds are in
`<savedir>/worlds_local`. See [Open a world that is not in the list](../guides/open-a-world.md).

**Seed.** The word a world is made from: the same seed gives the same land, biomes and locations. See
[A new world](../new-world.md).

**Snapping.** Pieces joining at their snap points, as with the game's hammer. See
[Snapping](../concepts/objects.md#snapping).

**Spawn point.** An invisible object of the game where creatures appear. See
[Spawn points](../concepts/objects.md#spawn-points).

**Stamp.** A brush shape made from a height picture, put into the ground whole or painted. See
[Sculpt and paint](../terrain.md).

**Support check.** The Workshop's colours for how well each piece is held up, as in the game's build mode. See
[The support check](../concepts/objects.md#the-support-check).

**Token.** The plugin's secret, in `BepInEx/config/Tie.WorldEditorBridge.cfg`, that the editor must give to connect.
See [How it connects](../concepts/live-editing.md#how-it-connects).

**Tunnel.** The encrypted SSH connection the editor opens to reach the plugin on a dedicated server. See
[Edit a dedicated server while people play](../guides/edit-a-server-live.md).

**View tool.** The tool (`Esc` or `H`) to look around and pick objects without changing anything. Not the same as the
**View** panel (`V`). See [The editor window](../editor-basics.md#the-tool-rail).

**Workshop.** A blank plot, outside any world, to build on and keep the building as a blueprint. See
[The Workshop](../workshop.md).

**World folder.** The folder that holds one world: `_main.<n>.chunks`, `.db2`, `.fwl2`, `.ok` and the `.chunk` files.
Single-player worlds are in `worlds_local`. See [Open a world that is not in the list](../guides/open-a-world.md).

**Zone.** A 64 × 64 m square of the world, the unit the game loads and saves. See
[Zones](../concepts/areas-and-zones.md#zones).
