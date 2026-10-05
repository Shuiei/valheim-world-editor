# Valheim World Editor

[![CircleCI](https://dl.circleci.com/status-badge/img/gh/Shuiei/valheim-world-editor/tree/main.svg?style=svg)](https://dl.circleci.com/status-badge/redirect/gh/Shuiei/valheim-world-editor/tree/main)

![The 3D editor showing a base, with the tool rail, tool panel and View panel](docs/images/overview.jpg)

## What is this?

A world editor for Valheim, in the spirit of Minecraft's MCEdit, WorldEdit and WorldPainter. It
opens a Valheim world in its own window, drawn with the game's own terrain, textures and models. There you can:

- **Shape the ground**: raise, lower, flatten, smooth, naturalize or restore it, and paint dirt,
  cultivated soil or paved stone.
- **Build roads and ramps** along a line you draw.
- **Work on whole areas**: level, paint, clear or copy and paste a box or polygon, or have the game
  generate zones again from scratch.
- **Place anything the game has**: trees, rocks, bushes, crops, building pieces and so on, with
  brush, line, grid and zone patterns that respect the game's growing rules.
- **Select, move, turn, copy and delete objects**, including whole buildings.
- **Measure** distances and slopes, and see the ground coloured by steepness.
- **Undo anything**, with a history panel that can roll back to any point or remove one change.

There are three ways to edit, all on the start page:

| Way | What it edits | Needs |
|---|---|---|
| **My game** (live) | The world you are playing in: single player, or the one you host. You see the changes in game right away. | BepInEx and the WorldEditorBridge plugin in your Valheim. |
| **A dedicated server** (live) | Your server's world while people play. The editor logs in to the server and makes its own tunnel. | BepInEx and the plugin on the server, and an SSH login to it. |
| **A saved world** (offline) | World files on this computer, with the game closed. Saved as a new save, with a full backup first. | Nothing. |

It is a single program for Windows and Linux: download, unpack, double-click. Everything runs on
your own computer; nothing is sent anywhere else.

## Installation

### What you need

- **Windows 10/11 or Linux**, 64-bit.
- **Valheim** installed through Steam on the same computer, for the game's look. The editor finds it
  by itself and copies the textures and models it needs from it, once. Without it the editor still
  works, with plain colours and no object models.
- For the **live** ways: **BepInEx** in the game that hosts the world (your Valheim, or the
  server), using [BepInExPack for Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/),
  plus the WorldEditorBridge plugin that comes with the editor. Players who join need neither.

Nothing else: no Python, no .NET, no `ssh` command. Everything the editor needs comes in the
download. Its window uses the web engine of the system: Microsoft Edge WebView2 on Windows (part of
Windows 11 and of an up-to-date Windows 10) and WebKitGTK on Linux (installed with most desktops).
Without it, the editor opens in your web browser instead.

### Install and start

1. Download `ValheimWorldEditor-<version>-win-x64.zip` (Windows) or
   `ValheimWorldEditor-<version>-linux-x64.tar.gz` (Linux) from the
   [Releases](https://github.com/Shuiei/valheim-world-editor/releases) page.
2. Unpack it anywhere (Desktop, Documents…). It holds the editor, its `README.txt`, and a `plugin`
   folder with `WorldEditorBridge.dll` and its own `README.txt`.
3. Double-click **ValheimWorldEditor** (`ValheimWorldEditor.exe` on Windows). It opens in its own
   window, on the start page. (Windows may say "Windows protected your PC" the first time: the
   program is not signed. Click "More info", then "Run anyway".)

![The start page](docs/images/start-game.jpg)

The first time, a bar at the top shows the editor copying the game's look from your Valheim install
(a few minutes, about 150 MB); you can already start editing meanwhile. After a Valheim update it is
copied again by itself. If Valheim is not found, the bar asks for its folder (the one Steam installed
it into, with `valheim_Data`).

### My game (live)

1. **Install BepInEx** in your Valheim, once: [BepInExPack for Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/),
   either with a mod manager (r2modman, Thunderstore Mod Manager) or by hand into the Valheim folder,
   following its page.
2. **Add the plugin:** with a mod manager, install
   [ValheimWorldEditor_Windows](https://thunderstore.io/c/valheim/p/Tie/ValheimWorldEditor_Windows/) or
   [ValheimWorldEditor_Linux](https://thunderstore.io/c/valheim/p/Tie/ValheimWorldEditor_Linux/)
   from Thunderstore: the editor, with the plugin and BepInEx installed alongside (start the editor
   from the profile's folder, see its page). By hand: copy `plugin/WorldEditorBridge.dll` from the
   editor's folder into `BepInEx/plugins` of your Valheim.
3. **Start Valheim** with BepInEx and load your world (single player, or start a server from the
   game to host it).
4. On the start page, **My game** shows "Valheim is running with the world …": click **Edit live**.
   On your own computer it finds the plugin and its token by itself, also in mod manager profiles.

Edit, then **Apply live** (or switch on **Auto** to apply every change right away): you see it in
game at once, and the game saves it as usual.

### A dedicated server (live)

![Connecting to a server](docs/images/start-server.jpg)

1. **On the server, once:** install BepInEx (BepInExPack for Valheim, following its instructions
   for dedicated servers), copy `plugin/WorldEditorBridge.dll` into its `BepInEx/plugins` (or
   install [WorldEditorBridge](https://thunderstore.io/c/valheim/p/Tie/WorldEditorBridge/) in a
   mod manager's server profile), and restart it. See `plugin/README.txt` in the download.
2. On the start page, **A dedicated server**: enter the server's address, the user you log in to it
   with (SSH), the password or an SSH key file, and the **plugin token**: the `Token` line of
   `BepInEx/config/local.worldeditorbridge.cfg` on the server. Tick **Save password** to not type the
   password again.
3. **Connect.** The editor logs in, opens its own encrypted tunnel to the plugin and loads the
   world. The plugin only listens on the server itself, so it is never exposed to the internet.
4. The server is then **saved** in the list, with its token: next time it is one click.

If something is wrong, the editor says what: login refused, no answer, wrong token, plugin not
running on the server, or a server whose identity changed since last time (it refuses to connect
then). Already
have your own tunnel (`ssh -L`, PuTTY)? Use **More options → I made my own tunnel**. More in
[docs/live-mode.md](docs/live-mode.md).

### A saved world (offline)

![Saved worlds](docs/images/start-offline.jpg)

1. **Close Valheim, or stop the server** that uses the world. A running game saves over the files
   the editor writes.
2. On the start page, **A saved world**: click the world. Worlds in Valheim's usual folders are listed
   by themselves; for another one (a copy of a server's world, for example) use **Another world
   folder** with **Browse…** or by typing it (`<savedir>/worlds_local/<World>` for a server).
3. Edit, then **Save to world**. The editor first copies the whole world folder to
   `<World>_backup_terraineditor-<date>`, then writes your changes as the next save and reads them
   back to check them. Start the game or server again to see the result.

In every way, the [world map](docs/map.md) opens first: click a spot and choose **Edit in 3D**.
**Worlds** (on the map page) goes back to the start page.

### Game, plugin and worlds not where they usually are

![Settings](docs/images/settings.jpg)

The editor looks in the usual places by itself: Valheim in every Steam library (also Flatpak Steam
and extra libraries), BepInEx in the Valheim folder and in the default r2modman / Thunderstore Mod
Manager profiles, and worlds in Valheim's own world folders (Proton's too). When yours are
elsewhere, set them in **⚙ Settings** on the start page:

- **Valheim game folder**: the folder with `valheim_Data` (a copy outside Steam, a second install…).
  **Automatic** goes back to searching the Steam libraries.
- **BepInEx folders**: a `BepInEx` folder, a mod manager profile, or a folder of profiles (a mod
  manager with its own data folder). Used to find the plugin for **My game**.
- **World folders**: a world, or a folder of worlds (a server's `<savedir>/worlds_local` copy, a
  backup folder…). Always listed under **A saved world**.

For a **dedicated server**, the server form has an optional **Server's Valheim folder** (the
folder with its `BepInEx`, for example `/home/valheim/server`). With it the editor reads the
plugin's port from the server and, when the plugin does not answer, says exactly why: no BepInEx
there, no plugin in `BepInEx/plugins`, or a server that is not running with BepInEx. It is saved
with the server; **edit** next to a saved server changes it. (The token is still always typed.)

### Where things are kept

Settings, saved servers (`servers.cfg`), the copied game files and a log are in `~/.local/share/ValheimWorldEditor` (Linux) or
`%LOCALAPPDATA%\ValheimWorldEditor` (Windows). The bottom of the start page shows the folder.

### Command line (optional)

The program also takes a world or a live server directly, which is handy for scripts:

```sh
ValheimWorldEditor "<world folder>"                                   # open that world
ValheimWorldEditor --live http://127.0.0.1:5182 --token <token>       # connect to a server
ValheimWorldEditor --browser                                          # use the web browser instead of a window
ValheimWorldEditor --port 5190                                        # a fixed port (default: the first free one from 5180)
```

### Building from source

```sh
dotnet publish TerrainEditor.csproj -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true -o ../ValheimWorldEditor
```

Use `-r win-x64` for Windows. Publish outside the source folder: a folder inside it would be packed
into the next build. `tools/release.sh <folder>` builds the complete release packages
(program, page, the exporter and its Python runtime). See [docs/development.md](docs/development.md).

## Documentation

| Page | Covers |
|---|---|
| [Getting around](docs/editor-basics.md) | Camera, top bar, saving, discarding, history, View panel, shortcuts |
| [World map](docs/map.md) | The overview map and opening an area in 3D |
| [Ground tools](docs/terrain.md) | Raise, Lower, Flatten, Smooth, Naturalize, Restore, ground paint |
| [Path](docs/path.md) | Roads, ramps and paint along a line |
| [Mask](docs/masks.md) | Limiting any tool by biome, height, slope or paint |
| [Area, copy and paste](docs/area.md) | Box and polygon selections, bulk actions, paste, zone reset |
| [Plant](docs/plant.md) | Brush, Line, Grid and Zone placement, any kind of object |
| [Select](docs/select.md) | Selecting, moving with arrows, turning, dropping, copying, deleting |
| [Measure and overlays](docs/measure.md) | Distances, slopes, slope colours, height lines |
| [Live mode](docs/live-mode.md) | Editing a running server |
| [Development](docs/development.md) | Project layout, extracting the game files, tests |

What changed and when: [CHANGELOG.md](CHANGELOG.md).

## Safety

- Offline saves always make a full backup of the world folder first, and the save is read back and
  checked before the editor reports success.
- The editor follows the game's own limits: ground moves at most 8 m from its original height, and
  the edge of the loaded area is locked so it joins its neighbours seamlessly.
- Nothing is written until you press **Save to world** or **Apply live**; **Discard** undoes
  everything that is not saved or applied yet.
- Keep your own copies of worlds you care about anyway.
