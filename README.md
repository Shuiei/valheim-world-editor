# Valheim World Editor

![The 3D editor showing a base, with the tool rail, tool panel and View panel](docs/images/overview.jpg)

## What is this?

A world editor for Valheim, in the spirit of Minecraft's MCEdit, WorldEdit and WorldPainter. It
opens a Valheim world and shows it in your browser, drawn with the game's own terrain, textures and
models. There you can:

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

It works in two ways:

| Mode | What it edits | When to use it |
|---|---|---|
| **Offline** | A world save on disk (single player world, or a server's world folder). | The game or server is stopped. Changes are written as a new save, with a full backup first. |
| **Online (live)** | The world of a running game or dedicated server, through the WorldEditorBridge plugin. | Players can stay connected; they see your changes as soon as you apply them. |

The editor is a small program (C#, .NET 8) that serves the editing page on your own computer;
nothing is sent anywhere else.

## Installation

### What you need

- **Linux x64** for the ready-made program. For Windows, build it from source (see
  [Building from source](#building-from-source)); the Windows build has only been tried under Wine,
  where editing and saving work but the world map did not load.
- A browser with WebGL 2 (any current Firefox, Chrome or Edge).
- A Valheim world in the current chunked save format (world version 41: a world folder with
  `_main.<n>.chunks` and `*.chunk` files). The older single `.db` file format is not supported.
- Optional: the files extracted from the game into `wwwroot/` (textures, shaders and models) for the
  in-game look. The ready-made `ValheimTerrainEditor` folder already contains them; they are not in
  git because they belong to the game. Without them the map and the 3D editor use plain colours,
  objects are not drawn (they can still be planted and saved), and buildings are boxes.

### Offline mode

1. **Get the editor.** Copy the `ValheimTerrainEditor` folder (the `ValheimTerrainEditor` program
   and its `wwwroot` folder) anywhere you like, or build it from source.
2. **Close the game or stop the server** that uses the world. A running game saves over the files
   the editor writes.
3. **Find the world folder.** It is the `<World>` folder that holds `_main.<n>.*` and `*.chunk`
   files:
   - Single player on Linux: `~/.config/unity3d/IronGate/Valheim/worlds_local/<World>`
   - Single player on Windows: `%USERPROFILE%\AppData\LocalLow\IronGate\Valheim\worlds_local\<World>`
   - Dedicated server: the `worlds_local/<World>` folder inside the server's `-savedir`.
4. **Start the editor** with that folder:

   ```sh
   ./ValheimTerrainEditor "/path/to/worlds_local/MyWorld" --port 5180
   ```

5. **Open http://127.0.0.1:5180** in your browser. You get the [world map](docs/map.md); click a spot
   and choose **Edit in 3D**.
6. Edit, then press **Save to world**. The editor first copies the whole world folder to
   `<World>_backup_terraineditor-<date>`, then writes your changes as the next save number and reads
   them back to check them. Start the game or server again to see the result.

### Online (live) mode

Live mode edits the world of a running game. It needs the **WorldEditorBridge** plugin in the game
(normally the dedicated server), and BepInEx installed there.

1. **Install the plugin.** Copy `WorldEditorBridge.dll` into the server's `BepInEx/plugins/` folder
   and restart the server.
2. **Get the token.** On its first start the plugin writes
   `BepInEx/config/local.worldeditorbridge.cfg` with a random `Token`. Keep it secret: anyone with the
   token and access to the port can change the world. The same file sets the port (default 5182) and
   the address (default `127.0.0.1`, reachable only from the server itself).
3. **Open a tunnel** from your computer to the server, so the bridge stays private:

   ```sh
   ssh -N -L 5182:127.0.0.1:5182 user@your-server
   ```

   When the game runs on your own computer (single player, or host and play), skip this step.
4. **Start the editor in live mode:**

   ```sh
   ./ValheimTerrainEditor --live http://127.0.0.1:5182 --token <token> --port 5181
   ```

   The token can also come from the `WORLD_BRIDGE_TOKEN` environment variable.
5. **Open http://127.0.0.1:5181.** A green **LIVE** badge shows in the top bar. Edit as usual, then
   press **Apply live** (or switch on **Auto** to apply every change right away). The game shows
   the changes to every player and saves them as usual.

More in [docs/live-mode.md](docs/live-mode.md): what is applied, Reload, and limits.

### Building from source

```sh
dotnet publish TerrainEditor.csproj -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true -o ../ValheimTerrainEditor
```

`wwwroot` is copied into the output by the build. Publish outside the source folder (as above): a
folder inside it would be packed into the next build. Use `-r win-x64` for Windows. Then copy the
extracted game files into the output's `wwwroot` (see [docs/development.md](docs/development.md)).

The plugin builds separately (`plugin/WorldEditorBridge`, .NET Framework 4.7.2, against the game's
and BepInEx's DLLs; adjust the `HintPath`s in its project file).

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
