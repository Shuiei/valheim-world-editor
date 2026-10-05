# Valheim world editor

Offline editor for Valheim world saves (chunked format, world version 41): a C# (.NET 8) server
with a browser UI. It reads `_main.<n>.*` and the chunk files, shows the world map and a 3D editor
with the game's own terrain shader, textures and models, and writes changes back as a new save
number (with a full backup and a read-back check).

- `Program.cs` – web server and API (`/api/world`, `/api/region`, `/api/objects`, `/api/save`, …)
- `Save/` – save reader (`WorldSave`), writer (`WorldWriter`), object templates and the `.db2` zone list
- `Editing/` – pending changes (terrain zones, deleted / added objects, zone resets)
- `WorldGen/` – port of Valheim's world generator (bit-exact base terrain) and map data
- `wwwroot/` – map (`index.html`) and 3D editor (`editor.html`, `terrain/*.js`)
- `plugin/WorldEditorBridge/` – BepInEx plugin for live mode (net472, built separately)
- `tools/asset-export/` – UnityPy scripts that extract textures, shaders and models from the game
  into `wwwroot/` (those files are not in git)

Build: `dotnet publish -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true -o <dir>`.
Run: `ValheimTerrainEditor [worldDir] [--port 5180]`, then open http://127.0.0.1:5180.
Close Valheim (game and server) before saving into a world.

## Live mode (WorldEditorBridge)

`plugin/WorldEditorBridge` runs inside the game (normally on the dedicated server) and serves a
snapshot of the running world on `127.0.0.1:5182`, protected by a token (generated on first start
in `BepInEx/config/local.worldeditorbridge.cfg`).

1. Copy `WorldEditorBridge.dll` to the server's `BepInEx/plugins/` and restart the server.
2. Open a tunnel from your PC: `ssh -L 5182:127.0.0.1:5182 <user>@<server>`.
3. Run the editor: `ValheimTerrainEditor --live http://127.0.0.1:5182 --token <token> --port 5181`.

The editor then shows the live world and its players; "Reload" fetches a new snapshot.
"Apply live" (or "Auto", after every stroke and undo) sends the changed zones' ground to the game,
which syncs them to every player and saves them as usual. Object changes and zone resets are not
applied live yet. The plugin also works in a game that hosts its own world (single player or
host-and-play), with no tunnel; it refuses to run in a game that joined someone else's server.
