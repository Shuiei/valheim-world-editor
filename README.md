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
- `tools/asset-export/` – UnityPy scripts that extract textures, shaders and models from the game
  into `wwwroot/` (those files are not in git)

Build: `dotnet publish -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true -o <dir>`.
Run: `ValheimTerrainEditor [worldDir] [--port 5180]`, then open http://127.0.0.1:5180.
Close Valheim (game and server) before saving into a world.
