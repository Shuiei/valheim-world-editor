# DrawnWorlds

Play the worlds you draw with [Valheim World Editor](https://github.com/Shuiei/valheim-world-editor)'s **Draw a
world**: paint each biome, the seas, lakes and rivers and the start point on a map, and the game makes that world.

- The land and the biomes are the drawing's. Everything else is the game's own: each biome's hills and rocks, the
  rivers and lakes it adds, the forests, and where it puts the start temple, the bosses' altars, the traders and the
  dungeons, each on its own biome.
- The drawing travels inside the world's file: copy the world folder and it comes along. The server sends it to every
  player as they join.

## Install

On the server **and on every player's game** (each game makes its own ground):

1. Install [BepInExPack for Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/).
2. Install DrawnWorlds with a mod manager, or copy `DrawnWorlds.dll` into `BepInEx/plugins`.

The game without the mod refuses a drawn world rather than making another one, and a server with it turns away players
without it. Worlds that were not drawn are not changed.

## Make a drawn world

In Valheim World Editor: start page, **A new world** → **Draw a world**. Draw, look at the world it makes, then
**Create world**. See [Draw a world](https://github.com/Shuiei/valheim-world-editor/blob/main/docs/draw-world.md).
