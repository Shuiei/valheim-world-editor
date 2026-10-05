Valheim World Editor @VERSION@
=============================

A world editor for Valheim that runs in your browser: shape the ground, paint it, build paths,
place and move trees, rocks, crops and building pieces, copy and paste areas, and more.
Full documentation: https://github.com/Shuiei/valheim-world-editor (README and docs/).

This folder holds:
  ValheimTerrainEditor(.exe)   the editor (a small local web server; nothing is sent anywhere)
  wwwroot/                     the editing page
  export-game-files/           a tool that copies the game's textures and models from YOUR Valheim
                               install, for the in-game look (they are not included: they belong
                               to the game)
The server plugin for live mode is the separate download WorldEditorBridge.dll.


1. Optional, once: copy the game's look from your Valheim install
------------------------------------------------------------------
Without this step the editor works, with plain colours and no object models.

  python3 -m pip install -r export-game-files/requirements.txt
  python3 export-game-files/export_all.py --valheim "<Valheim game folder>" --out wwwroot

  The Valheim game folder is the one with valheim_Data, for example
    Linux (Steam):   ~/.local/share/Steam/steamapps/common/Valheim
    Windows (Steam): C:\Program Files (x86)\Steam\steamapps\common\Valheim
  It takes a few minutes and writes about 150 MB into wwwroot. By default every kind of object
  gets a model; --objects none exports only building pieces, --objects world --world <world
  folder> only what that world has. If it is interrupted, run it again: it continues.


2a. Offline: edit a world save
------------------------------
  1. Close the game / stop the server that uses the world.
  2. Start the editor with the world folder (the <World> folder with _main.<n>.* and *.chunk files):
       Linux:    ./ValheimTerrainEditor "$HOME/.config/unity3d/IronGate/Valheim/worlds_local/MyWorld"
       Windows:  ValheimTerrainEditor.exe "%USERPROFILE%\AppData\LocalLow\IronGate\Valheim\worlds_local\MyWorld"
     Add --port <n> to use another port than 5180.
  3. Open http://127.0.0.1:5180, click a spot on the map, "Edit in 3D".
  4. "Save to world" first copies the whole world folder to <World>_backup_terraineditor-<date>,
     then writes the changes as the next save.


2b. Online (live): edit a running server
----------------------------------------
  Live mode needs BepInEx (the mod loader) on the server: BepInExPack for Valheim,
  https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/ . Players who join do not
  need it, and offline mode does not need it at all.
  1. On the server, with BepInEx installed: copy WorldEditorBridge.dll to BepInEx/plugins/ and
     restart the server.
  2. Read the Token in BepInEx/config/local.worldeditorbridge.cfg (keep it secret).
  3. On your computer, open a tunnel:  ssh -N -L 5182:127.0.0.1:5182 user@your-server
     (not needed when the game runs on your own computer).
  4. Start:  ./ValheimTerrainEditor --live http://127.0.0.1:5182 --token <token> --port 5181
  5. Open http://127.0.0.1:5181, edit, then "Apply live" (or switch on "Auto").


Notes
-----
- Every control in the editor explains itself when you hover it; "?" lists the shortcuts.
- The Windows build has only been tried under Wine (editing and saving work there; the world map
  did not load under Wine). Reports welcome.
- Keep your own backups of worlds you care about.
