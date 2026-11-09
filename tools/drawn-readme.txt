DrawnWorlds @VERSION@: drawn worlds in the game (Valheim World Editor @VERSION@)
=========================================================================

DrawnWorlds lets the game play worlds drawn with Valheim World Editor's Draw a world: the biomes, seas, lakes, rivers
and start you painted, with the game's own rules for everything else.

It goes on the server AND on every player's game: each game makes its own ground. The game without it refuses a drawn
world, and a server with it turns away players without it. Worlds that were not drawn are not changed.


1. Install BepInEx (once)
-------------------------
  BepInExPack for Valheim: https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/
  Start the game or server once: BepInEx creates the folders BepInEx/plugins and BepInEx/config.


2. Install the plugin
---------------------
  1. Copy DrawnWorlds.dll (in the plugins folder of this zip) into BepInEx/plugins (of your Valheim, of your mod
     manager profile, or of the server).
  2. Restart the game or server. BepInEx/LogOutput.log then shows "DrawnWorlds @VERSION@: drawn worlds are read,
     played and saved as the editor drew them."


Remove it
---------
  Delete BepInEx/plugins/DrawnWorlds.dll. Drawn worlds then no longer open (the game says their version is not
  supported); they open again once it is back.
