Valheim World Editor @VERSION@ for macOS (experimental)
======================================================

A world editor for Valheim: shape the ground, paint it, build paths, place and move trees, rocks,
crops and building pieces, copy and paste areas, and more.
Documentation: https://github.com/Shuiei/valheim-world-editor

EXPERIMENTAL: this Mac build has not been tried on a Mac yet. If something does not work, please
open an issue (https://github.com/Shuiei/valheim-world-editor/issues) and attach the editor's log
(see "Good to know" below).

This download is for Macs with Apple silicon (M1 and later). Macs with an Intel processor are not
supported yet.


1. Start the editor
-------------------
  1. Unzip the download (double-click it) and move ValheimWorldEditor.app where you like, for
     example into Applications.
  2. The first time, macOS stops it: the editor is not signed with an Apple Developer certificate,
     so macOS cannot check who made it ("ValheimWorldEditor" cannot be opened / Apple could not
     verify it). To allow it, once:
       - Double-click ValheimWorldEditor.app, then close the message.
       - Open System Settings › Privacy & Security, scroll down to Security, and click
         "Open Anyway" next to ValheimWorldEditor. Confirm with your password.
     Or, in Terminal:   xattr -dr com.apple.quarantine /Applications/ValheimWorldEditor.app
     (with the path where you put it). After that it opens with a double-click.
  3. The editor opens in its own window, on the start page. It lists the worlds of this Mac.
     It reads the game's look (textures and models) from your Valheim install, nothing is copied:
     the first time (and after a Valheim update) it reads the game's files for a few seconds.
     Valheim is found in Steam's libraries. If it is not found, the start page asks for its folder:
     the one holding valheim.app, usually ~/Library/Application Support/Steam/steamapps/common/Valheim
     (in Steam: right-click Valheim, Manage › Browse local files).

  Nothing else to install. The editor draws with OpenGL, which every Mac has (macOS 12 or later).


2. Choose how to edit
--------------------
  The start page offers three ways.

  MY GAME (live): edit the world you are playing in, single player or the one you host, and see
  the changes in game right away.
    1. Once: install BepInEx for Valheim (BepInExPack for Valheim,
       https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/ ), with a mod manager such
       as r2modman or by hand, following its page.
    2. Once: the WorldEditorBridge plugin: install it with a mod manager (Tie-WorldEditorBridge on
       Thunderstore or Hexium), or copy WorldEditorBridge.dll from WorldEditorBridge-@NUMBER@.zip
       (on the same releases page as this download) into BepInEx/plugins of your Valheim.
    3. Start Valheim with BepInEx and load your world. The start page then shows
       "Valheim is running with the world ...": click "Edit live".

  A DEDICATED SERVER (live): edit your server's world while people play.
    1. Once, on the server: BepInEx and WorldEditorBridge.dll (from WorldEditorBridge-@NUMBER@.zip on
       the releases page, or Tie-WorldEditorBridge in a mod manager's server profile).
    2. Enter the server's address, your SSH user and password (or SSH key file), and the plugin's
       token (the Token line of BepInEx/config/Tie.WorldEditorBridge.cfg on the server).
       Tick "Save password" to not type it again.
    3. Connect. The editor logs in and makes its own encrypted tunnel: no ssh command needed.
       The server is then saved in the list: next time it is one click.

  A SAVED WORLD (offline): edit world files with the game closed.
    1. Close Valheim first (or stop the server): a running game writes over the files.
    2. Click the world. Single-player worlds are in
       ~/Library/Application Support/IronGate/Valheim/worlds_local and are listed by themselves;
       "Another world folder" opens any other (it holds _main.<n>.chunks files).
    3. "Save to world" writes the changes as the world's next save and reads them back to check them.
       Start the game again to see them.

  In every way the world map opens first: click a spot and choose "Edit in 3D". "Worlds" (on the
  map page) goes back to the start page. Live changes are sent with "Apply live" (or "Auto").


Good to know
------------
  - Cmd works wherever the documentation says Ctrl (Cmd+Z undoes, Cmd+click adds to a selection).
  - Game, BepInEx profile or worlds somewhere unusual? "Settings" on the start page.
  - Every control explains itself when you hover it; "?" lists the keyboard shortcuts.
  - Settings, saved servers (servers.cfg) and the log (ValheimWorldEditor.log):
    ~/Library/Application Support/ValheimWorldEditor ("Open log" at the bottom of the start page's menu).
  - Add-ons (editor support for a mod, each its own download) go in the addons folder next to
    ValheimWorldEditor.app: see addons/README.txt.
  - Keep your own backups of worlds you care about.
