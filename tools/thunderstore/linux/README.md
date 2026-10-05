# Valheim World Editor (Linux)

A world editor for Valheim, in the spirit of Minecraft's MCEdit and WorldPainter, drawn with the
game's own terrain, textures and models. This package holds the editor program for **Linux** and
the **WorldEditorBridge** plugin that lets it edit a world while the game runs. (On Windows: the
ValheimWorldEditor_Windows package.)

![The 3D editor showing a base](https://raw.githubusercontent.com/Shuiei/valheim-world-editor/main/docs/images/overview.jpg)

## What it does

- **Shape the ground**: raise, lower, flatten, smooth, naturalize or restore it, and paint dirt,
  cultivated soil or paved stone.
- **Build roads and ramps** along a line you draw.
- **Work on whole areas**: level, paint, clear, copy and paste, or have the game generate zones
  again.
- **Place anything the game has**: trees, rocks, bushes, crops, building pieces, with brush, line,
  grid and zone patterns that respect the game's growing rules.
- **Select, move, turn, copy and delete objects**, including whole buildings.
- **Undo anything**, with a history panel.

## Starting the editor

The mod manager installs it into your profile; it is a program you start yourself, next to the
game:

1. In the mod manager, open the profile's folder (r2modman: **Settings → Browse profile folder**).
2. Go to `BepInEx/plugins/<author>-ValheimWorldEditor_Linux/ValheimWorldEditor/` and start
   **ValheimWorldEditor**.
   Mod managers unpack files without their Linux permissions: the first time, make the program
   runnable (in that folder: `chmod +x ValheimWorldEditor`, or Properties → "Allow executing").

The first start copies the game's look (textures and models) from your Valheim install, once, in
a few minutes. Valheim must be installed through Steam on the same computer.

## Three ways to edit

- **My game (live)**: start Valheim from the mod manager and load your world (single player, or
  hosting). On the start page, **My game** finds it and its plugin (also in this profile): click
  **Edit live**, then **Apply live**, and see the changes in game right away.
- **A dedicated server (live)**: install this package or the plugin-only **WorldEditorBridge**
  package on the server too. On the start page, **A dedicated server**: the server's address, your
  SSH login and the plugin's Token (in `BepInEx/config/local.worldeditorbridge.cfg` on the
  server). The editor makes its own encrypted tunnel.
- **A saved world (offline)**: with the game closed, open a world file; a full backup is made
  before every save.

Players who join a server need neither the plugin nor BepInEx.

## More

- Full guide, with screenshots of every tool:
  [github.com/Shuiei/valheim-world-editor](https://github.com/Shuiei/valheim-world-editor)
- Live mode in detail:
  [docs/live-mode.md](https://github.com/Shuiei/valheim-world-editor/blob/main/docs/live-mode.md)
- Problems and ideas: [issues](https://github.com/Shuiei/valheim-world-editor/issues)
