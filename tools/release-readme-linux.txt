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

  Nothing else to install. The window uses WebKitGTK (libwebkit2gtk-4.1, installed with most
  desktops); without it the editor opens in your web browser instead.


2. Edit a world saved on this computer (offline)
------------------------------------------------
  1. Close Valheim first (or stop the server that uses the world): a running game writes over
     the files.
  2. On the start page, click the world. Single-player worlds are in
     ~/.config/unity3d/IronGate/Valheim/worlds_local (and inside Steam's Proton folder) and are
     listed by themselves. For a dedicated server's world, use "Another world folder" and
     "Browse...": pick <savedir>/worlds_local/<World> (it holds _main.<n>.chunks files).
  3. On the world map, click a spot and choose "Edit in 3D".
  4. "Save to world" first copies the whole world folder to <World>_backup_terraineditor-<date>,
     then writes the changes. Start the game or server again to see them.
  "Worlds" (on the map page) goes back to the start page.


3. Edit a running server (live)
-------------------------------
  The server needs BepInEx and the WorldEditorBridge plugin: see server-plugin/README.txt in this
  folder. Then:
  1. Open a tunnel to the server (keep it running while you edit):
         ssh -N -L 5182:127.0.0.1:5182 user@your-server
  2. On the start page, under "A running server (live)", enter 127.0.0.1:5182 and the Token,
     then Connect. If something is wrong, the editor says what within 10 seconds.
  3. Edit, then "Apply live" (or switch on "Auto"): players see the changes right away.


Good to know
------------
  - Every control explains itself when you hover it; "?" lists the keyboard shortcuts.
  - Settings, the copied game files and the log: ~/.local/share/ValheimWorldEditor
  - Command line: ./ValheimWorldEditor "<world folder>" opens a world directly, --browser uses the
    web browser instead of a window.
  - Keep your own backups of worlds you care about.
