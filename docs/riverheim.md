# Riverheim

[Riverheim](https://thunderstore.io/c/valheim/p/Gurebu/Riverheim/) (by Gurebu) replaces the game's terrain and
biome generation: larger continents, forking rivers, mountains and lakes. A world made with it can only be drawn
by Riverheim itself: the game without the mod does not open it (it shows `[BAD_VERSION]`), and neither does the
editor without it.

## What the editor does with a Riverheim world

- The worlds list tags it **Riverheim** on its card.
- Opening it needs the **Riverheim** switch in Settings on and Riverheim installed in a BepInEx folder the editor
  knows (the Valheim folder, the editor's own profile, the folders added in Settings, r2modman's and Thunderstore Mod
  Manager's profiles, or the Expand World folder). The editor draws the world with your own copy of Riverheim: it
  carries none of the mod. It also needs the Valheim folder (Settings), as for any world: Riverheim's file names a
  few of the game's Unity files, which are read from there.
- The ground of the zones the game has not made yet, the biomes, the ground's paint, the forests, the map and the
  predictions (locations, regrowth) are Riverheim's, as the game makes them with the mod: checked point by point
  against a server running Riverheim 1.2.1.
- Opening takes a few seconds more: Riverheim lays the whole world out first, as the game does when it loads it.
- When it cannot be opened, the card or the message says why: the switch is off, Riverheim is not installed anywhere
  the editor looks, the world was made with Riverheim's deprecated beta (no Riverheim release opens those), the world
  file has a Riverheim setting this editor does not know, your Riverheim does not know the world's preset (an older
  Riverheim than the one that made it: update it), or your Riverheim could not lay the world out (the log says why).

Worlds made without Riverheim never use it, even with the switch on and the mod installed.

## With Expand World

Riverheim's world can be played with the Expand World mods. With the Expand World switch on too, the editor
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
