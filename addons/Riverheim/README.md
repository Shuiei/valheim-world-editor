# Riverheim add-on

An add-on for the Valheim World Editor that opens worlds made with
[Riverheim](https://thunderstore.io/c/valheim/p/Gurebu/Riverheim/) (by Gurebu), the mod that replaces the game's
terrain and biome generation: larger continents, forking rivers, mountains and lakes. A world made with it can only be
drawn by Riverheim itself: the game without the mod does not open it (it shows `[BAD_VERSION]`), and neither does the
editor without this add-on.

Not made by Riverheim's author: the add-on calls your own copy of Riverheim, the one in your BepInEx folder, and
carries none of the mod; this repository holds none of its code.

## Install

1. The Valheim World Editor 1.104.0 or later; 1.119.3 or later for the worlds Riverheim 1.3 and later save (in
   Worldhalla's own header).
2. From the editor's [Releases](https://github.com/Shuiei/valheim-world-editor/releases) page,
   `ValheimWorldEditor-addon-Riverheim-<version>.zip`: unpack it into the `addons` folder next to the editor (on
   macOS, next to `ValheimWorldEditor.app`), so that you have `addons/Riverheim/worldeditor.json`.
3. The add-on is used while Riverheim (`Riverheim.dll`) is in one of the BepInEx plugins folders the editor knows.
   **Settings → Add-ons** lists it, with its switch: off, Riverheim worlds are refused, as the game without the mod
   refuses them. Riverheim 1.3 and later are a plugin of [Worldhalla](https://thunderstore.io/c/valheim/p/Gurebu/Worldhalla/)
   (by Gurebu too), which must be in the same BepInEx folder, as the game needs it.

## What it does

- The worlds list tags it **Riverheim** on its card.
- Opening it needs the add-on on (Settings → Add-ons) and Riverheim installed in a BepInEx folder the editor knows
  (the Valheim folder, the editor's own profile, the folders added in Settings, r2modman's and Thunderstore Mod
  Manager's profiles). The editor draws the world with your own copy of Riverheim: it
  carries none of the mod. It also needs the Valheim folder (Settings), as for any world: Riverheim's file names a
  few of the game's Unity files, which are read from there.
- The ground of the zones the game has not made yet, the biomes, the ground's paint, the forests, the map and the
  predictions (locations, regrowth) are Riverheim's, as the game makes them with the mod: checked point by point
  against a server running Riverheim 1.3.1 with Worldhalla 1.0.0, as on 1.2.1 before it. Riverheim 1.3 lays worlds out
  as 1.2 did; Worldhalla, which answers the game for it since then, works the forest factor out from float values, a
  bit or two apart at a third of the points, and the add-on does the same for a Riverheim of 1.3 or later.
- Riverheim 1.3 and later keep a world's settings in Worldhalla's header, which they give every world they make, and
  an older Riverheim world on its first save: the editor reads both. A new world the editor makes gets Riverheim's own
  header, which every Riverheim release opens (Worldhalla turns it into its own on the first save).
- Opening takes a few seconds more: Riverheim lays the whole world out first, as the game does when it loads it.
- When it cannot be opened, its card stays in the list, greyed out, and says why: the add-on is turned off (Settings →
  Add-ons) or missing, Riverheim is not installed anywhere the editor looks, the world was made with Riverheim's deprecated beta (no Riverheim release opens those), the world
  file has a Riverheim setting this editor does not know (or Worldhalla settings or a Worldhalla format newer than
  1.0's), your Riverheim does not know the world's preset (an older Riverheim than the one that made it: update it),
  your Riverheim 1.3 or later has no Worldhalla beside it, or your Riverheim could not be loaded (the log says why).
  That your Riverheim could not lay the world out, or is not the version a live game's players need, is said as the
  world opens.

Worlds made without Riverheim never use it, even with the add-on on and the mod installed.

## Live mode

A game or server running a Riverheim world is drawn the same way, with your own Riverheim: it must be the version
players who join need, the same as the game's since Riverheim 1.3 (Worldhalla compares the whole version), the same
release before it (the first two numbers of the version); else the editor says both versions and does not open the
world. A game on Riverheim 1.3 and yours on 1.2, or the other way round, are refused: the forest factor differs, and
1.2 cannot read the worlds 1.3 saves. See [Live mode](../../docs/live-mode.md#riverheim).

## New worlds

With the add-on on and Riverheim found, the [New world](../../docs/new-world.md) page offers Riverheim as the world's
generator (the game's own is the other choice). The seed's map, the start, the traders and bosses, the search and the
world made are then Riverheim's, with Riverheim's own preset, and the world opens in the game as a Riverheim world.
Each seed takes a few seconds to look at: Riverheim lays its whole world out first, and a search looks at two seeds
at a time.

## With Expand World

Riverheim's world can be played with the Expand World mods. With the [Expand World add-on](../ExpandWorld/README.md) too, the editor
does what the game does with both, setting by setting (checked on a server running both):

| Expand World setting | On a Riverheim world |
|---|---|
| Size: Stretch world | Applies: Riverheim's world is stretched. |
| Size: World radius and edge | Riverheim's world ends at 10500 m times the stretch whatever they say; they still size the map, the save grid and where locations go. Riverheim asks for radius + edge = 10500 × stretch (its README's example: radius 20500, edge 500, stretch 2, biomes 2). |
| Size: Stretch biomes | No effect on Riverheim's biomes. |
| Size: Water depth multiplier, Forest multiplier | Apply, on Riverheim's ground and forests. |
| Size: Altitude multiplier and delta, Offset X/Y, Height variation seed | No effect. |
| Size: Locations, save grid, minimap | As without Riverheim. |
| Data: World data, Biome data | Off (their default): no effect. On: Data's world rules choose the biomes over Riverheim's, on Riverheim's ground (Riverheim advises turning them off). |
| Data: locations, vegetation, alt biomes, dungeons, rooms, blueprints | As without Riverheim. |
| Rivers: lakes, rivers, streams | Not laid out: Riverheim has its own. |
| Rivers: water level | Applies (and is the level Size's water depth works from). |

When a setting does something else on a Riverheim world than on the game's, the log says so as the world opens.

## For developers

The add-on (`src/Riverheim.WorldEditor`, net10.0, as the editor runs) registers Riverheim's world generator with the
editor's SDK (`IWorldGenerator`, see the editor's [modding guide](../../docs/modding.md#world-generators)), under
Riverheim's own id and Worldhalla's (`worldhalla:dev.gurebu.riverheim`): the header Riverheim keeps in a world file
(or the settings it keeps in Worldhalla's), and your Riverheim.dll loaded in a context of its own (with BepInEx's core,
Worldhalla and the game's Managed folder for the assemblies its plugin classes name) to lay each world out. Its tests
(`tests/Riverheim.WorldEditor.Tests`) download the real Riverheim 1.3.1 with Worldhalla 1.0.0, and Riverheim 1.2.1, and
compare their ground with values recorded on a server running each (`fixtures/riverheim-reference.txt`,
`fixtures/riverheim-reference-1.2.1.txt`), to the bit. The add-on's version is `VERSION` here, its changes
`CHANGELOG.md`: it goes out with every editor release, under its own version.
