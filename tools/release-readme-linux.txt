Valheim World Editor @VERSION@ for Linux
========================================

A world editor for Valheim: shape the ground, paint it, build paths, place and move trees, rocks,
crops and building pieces, copy and paste areas, and more.
Documentation: https://github.com/Shuiei/valheim-world-editor


1. Start the editor
-------------------
  1. Unpack this folder anywhere (for example in your home folder or on the Desktop).
  2. Double-click ValheimWorldEditor. If your file manager opens it as text or asks what to do,
     choose "Run" (or right-click, "Run as program"). From a terminal: ./ValheimWorldEditor
  3. The editor opens in its own window, on the start page. It lists the worlds of this computer.
     The first time, a bar shows it copying the game's look (textures and models) from your
     Valheim install: a few minutes, only once (and again after a Valheim update). You can already
     open a world meanwhile.
     Valheim is found in any Steam library (also Flatpak Steam). If it is not found, the bar asks
     for its folder: the one with valheim_Data, usually ~/.local/share/Steam/steamapps/common/Valheim.

  Nothing else to install. The editor draws with OpenGL 3.3, which any graphics driver of the last
  ten years has (Mesa, AMD, NVIDIA, Intel).


2. Choose how to edit
--------------------
  The start page offers three ways.

  MY GAME (live): edit the world you are playing in, single player or the one you host, and see
  the changes in game right away.
    1. Once: install BepInEx for Valheim (BepInExPack for Valheim,
       https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/ ), with a mod manager such
       as r2modman or by hand, following its page.
    2. Once: the WorldEditorBridge plugin: install it with a mod manager (Tie-WorldEditorBridge on
       Thunderstore or Hexium), or copy WorldEditorBridge.dll from WorldEditorBridge-@VERSION@.zip
       (on the same releases page as this download) into BepInEx/plugins of your Valheim.
    3. Start Valheim with BepInEx and load your world. The start page then shows
       "Valheim is running with the world ...": click "Edit live".

  A DEDICATED SERVER (live): edit your server's world while people play.
    1. Once, on the server: BepInEx and WorldEditorBridge.dll (from WorldEditorBridge-@VERSION@.zip on
       the releases page, or Tie-WorldEditorBridge in a mod manager's server profile).
    2. Enter the server's address, your SSH user and password (or SSH key file), and the plugin's
       token (the Token line of BepInEx/config/Tie.WorldEditorBridge.cfg on the server).
       Tick "Save password" to not type it again.
    3. Connect. The editor logs in and makes its own encrypted tunnel: no ssh command needed.
       The server is then saved in the list: next time it is one click.

  A SAVED WORLD (offline): edit world files with the game closed.
    1. Close Valheim first (or stop the server): a running game writes over the files.
    2. Click the world. Single-player worlds are in ~/.config/unity3d/IronGate/Valheim/worlds_local
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
  - Settings, saved servers (servers.cfg), the copied game files and the log: ~/.local/share/ValheimWorldEditor
  - Command line: ./ValheimWorldEditor "<world folder>" opens a world directly;
    ./ValheimWorldEditor --live http://127.0.0.1:5182 --token <token> connects to a game.
  - Keep your own backups of worlds you care about.
