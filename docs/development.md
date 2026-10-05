# Development

## Project layout

| Path | What it holds |
|---|---|
| `Program.cs` | Web server and HTTP API (`/api/world`, `/api/region`, `/api/objects`, `/api/save`, …), command line. |
| `Save/` | Save reader (`WorldSave`, `ValheimReader`), writer (`WorldWriter`), object building (`ZdoTools`: copies and blank objects), the `.db2` zone list, and live mode (`LiveBridge`, `LiveSync`). |
| `Editing/` | Pending changes: terrain per zone, deleted and added objects, zone resets (`EditStore`). |
| `WorldGen/` | Port of Valheim's world generator (bit-exact base terrain), map data, location flattening, the build-piece catalogue (`pieces.json`) and the prefab catalogue (`prefabs.json`). |
| `wwwroot/` | The map (`index.html`, `mapview.js`) and the 3D editor (`editor.html`, `editor/*.js`, `terrain/*.js`, three.js in `lib/`). |
| `wwwroot/editor/tips.js` | The hover text of every control. Keep it and the docs in step. |
| `plugin/WorldEditorBridge/` | The BepInEx plugin for live mode (.NET Framework 4.7.2). |
| `tools/asset-export/` | Python (UnityPy) scripts that extract textures, shaders, models and catalogues from the game. |
| `tools/zdo_scan.py` | Minimal chunk reader, to check saved objects byte by byte. |
| `tools/tests/` | Browser tests (Puppeteer). |
| `tools/docs-screenshots/` | Scripts that take the screenshots in `docs/images/`. |

## Command line

```
ValheimTerrainEditor [worldFolder] [--port 5180]
ValheimTerrainEditor --live <bridge url> --token <token> [--port 5181]
```

Other options are for checking the world generator against the game and are not needed for
editing: `--summary`, `--inspect`, `--verify <dump>`, `--verify-ingame <file>`, and
`--selftest-save <copy under /tmp>` (it refuses any other folder).

## Building

```sh
dotnet publish TerrainEditor.csproj -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true -o out
cp -r wwwroot out/
```

The plugin: build `plugin/WorldEditorBridge/WorldEditorBridge.csproj` after pointing its
`HintPath`s at your BepInEx `core` folder and the game's `*_Data/Managed` folder.

## Files extracted from the game

The in-game look uses files that belong to the game, so they are not in git (see `.gitignore`):

- `wwwroot/terrain/*.png` and `heightmap.frag.glsl`: terrain textures and the converted terrain shader;
- `wwwroot/maptex/`: map textures;
- `wwwroot/models/`: building, tree, rock and bush models, their textures and `objects.json`.

They are made from a Valheim install with the scripts in `tools/asset-export` (Python 3,
[UnityPy](https://github.com/K0lb3/UnityPy), Pillow). Each script says at the top what it reads and
writes. The usual order:

1. `build_cab_index.py`: index of the game's asset bundles.
2. `index_pieces.py`, `index_objects.py`: which bundle holds each piece and each world object.
3. `export_pieces.py <out>` (with `INDEX=` the object index for world objects): meshes, textures,
   materials.
4. `fix_normals.py`, `fix_alpha.py`: convert normal maps and clean cut-out textures.
5. `scan_prefabs.py <out.json>`: the prefab catalogue (`WorldGen/prefabs.json`, which is in git).

Without the extracted files the 3D editor falls back to flat colours (View → Look says it could not
load the game look) and objects have no models; the world map needs the map textures.

## Tests

The browser tests in `tools/tests` run against a running editor on a **copy** of a world, never the
real one:

```sh
cp -r ~/worlds/MyWorld /tmp/world-copy
./ValheimTerrainEditor /tmp/world-copy --port 5191 &
node tools/tests/test-editor.mjs http://127.0.0.1:5191 /tmp/out
```

They need `npm install puppeteer`; headless Chrome draws with SwiftShader, so they are slow.

## Screenshots

`tools/docs-screenshots/` holds the scripts that took the pictures in `docs/images/`. They were made
for one particular world (places are given as world coordinates), so with another world change the
coordinates at the top of each script. Run them in order against an editor on a world copy:

```sh
for f in tools/docs-screenshots/[0-9]*.mjs; do node "$f" http://127.0.0.1:5191 docs/images; done
```
