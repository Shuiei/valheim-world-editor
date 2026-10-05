Valheim World Editor @VERSION@ for Windows
==========================================

A world editor for Valheim: shape the ground, paint it, build paths, place and move trees, rocks,
crops and building pieces, copy and paste areas, and more.
Documentation: https://github.com/Shuiei/valheim-world-editor


1. Start the editor
-------------------
  1. Unzip this folder anywhere (for example on the Desktop). Do not run it from inside the zip.
  2. Double-click ValheimWorldEditor.exe.
     Windows may show "Windows protected your PC" the first time (the program is not signed):
     click "More info", then "Run anyway".
  3. The editor opens in its own window, on the start page. It lists the worlds of this computer.
     The first time, a bar shows it copying the game's look (textures and models) from your
     Valheim install: a few minutes, only once (and again after a Valheim update). You can already
     open a world meanwhile.
     If Valheim is not found, the bar asks for its folder: the one Steam installed it into, with
     valheim_Data, usually C:\Program Files (x86)\Steam\steamapps\common\Valheim.

  Nothing else to install. The window uses Microsoft Edge WebView2, which comes with Windows 11 and
  an up-to-date Windows 10; without it the editor opens in your web browser instead.


2. Edit a world saved on this computer (offline)
------------------------------------------------
  1. Close Valheim first (or stop the server that uses the world): a running game writes over
     the files.
  2. On the start page, click the world. Single-player worlds are in
     %USERPROFILE%\AppData\LocalLow\IronGate\Valheim\worlds_local and are listed by themselves.
     For another world (a copy of a server's world, for example), use "Another world folder"
     and "Browse...": pick the world's folder (it holds _main.<n>.chunks files).
  3. On the world map, click a spot and choose "Edit in 3D".
  4. "Save to world" first copies the whole world folder to <World>_backup_terraineditor-<date>,
     then writes the changes. Start Valheim again to see them.
  "Worlds" (on the map page) goes back to the start page.


3. Edit a running server (live)
-------------------------------
  The server needs BepInEx and the WorldEditorBridge plugin: see server-plugin\README.txt in this
  folder. Then:
  1. Open a tunnel to the server (keep this window open while you edit). In PowerShell:
         ssh -N -L 5182:127.0.0.1:5182 user@your-server
     ssh comes with Windows 10 and 11 (PowerShell or Command Prompt). If Windows says "ssh is not
     recognized", turn it on: Settings > System > Optional features > Add a feature > OpenSSH Client.
     With PuTTY instead: Connection > SSH > Tunnels, Source port 5182, Destination 127.0.0.1:5182,
     Add, then open the session as usual.
  2. On the start page, under "A running server (live)", enter 127.0.0.1:5182 and the Token,
     then Connect. If something is wrong, the editor says what within 10 seconds.
  3. Edit, then "Apply live" (or switch on "Auto"): players see the changes right away.


Good to know
------------
  - Every control explains itself when you hover it; "?" lists the keyboard shortcuts.
  - Settings, the copied game files and the log: %LOCALAPPDATA%\ValheimWorldEditor
  - The Windows version has only been tried under Wine so far. Reports welcome.
  - Keep your own backups of worlds you care about.
