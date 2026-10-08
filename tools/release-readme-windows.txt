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


2. Choose how to edit
--------------------
  The start page offers three ways.

  MY GAME (live): edit the world you are playing in, single player or the one you host, and see
  the changes in game right away.
    1. Once: install BepInEx for Valheim (BepInExPack for Valheim,
       https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/ ), with a mod manager such
       as r2modman or by hand, following its page.
    2. Once: copy plugin\WorldEditorBridge.dll (in this folder) into BepInEx\plugins of your Valheim
       (or of your mod manager profile).
    3. Start Valheim with BepInEx and load your world. The start page then shows
       "Valheim is running with the world ...": click "Edit live".

  A DEDICATED SERVER (live): edit your server's world while people play.
    1. Once, on the server: BepInEx and WorldEditorBridge.dll, see plugin\README.txt.
    2. Enter the server's address, your SSH user and password (or SSH key file), and the plugin's
       token (the Token line of BepInEx/config/Tie.WorldEditorBridge.cfg on the server).
       Tick "Save password" to not type it again.
    3. Connect. The editor logs in and makes its own encrypted tunnel: no ssh command needed.
       The server is then saved in the list: next time it is one click.

  A SAVED WORLD (offline): edit world files with the game closed.
    1. Close Valheim first (or stop the server): a running game writes over the files.
    2. Click the world. Single-player worlds are in %USERPROFILE%\AppData\LocalLow\IronGate\Valheim\worlds_local
       and are listed by themselves; "Another world folder" opens any other (it holds
       _main.<n>.chunks files).
    3. "Save to world" first copies the whole world folder to <World>_backup_terraineditor-<date>,
       then writes the changes. Start the game again to see them.

  In every way the world map opens first: click a spot and choose "Edit in 3D". "Worlds" (on the
  map page) goes back to the start page. Live changes are sent with "Apply live" (or "Auto").


Good to know
------------
  - Game, BepInEx profile or worlds somewhere unusual? "Settings" on the start page. For a server,
    "Server's Valheim folder" on its form gives precise errors when the plugin does not answer.
  - Every control explains itself when you hover it; "?" lists the keyboard shortcuts.
  - Settings, saved servers (servers.cfg), the copied game files and the log: %LOCALAPPDATA%\ValheimWorldEditor
  - The Windows version has only been tried under Wine so far. Reports welcome.
  - Keep your own backups of worlds you care about.
