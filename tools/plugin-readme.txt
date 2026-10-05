WorldEditorBridge @VERSION@: the plugin for live editing (Valheim World Editor @VERSION@)
=================================================================================

WorldEditorBridge lets the editor change a world while the game runs: you (and the players on your
server) see the changes as soon as they are applied. It goes into the game that hosts the world:
your own Valheim (single player or hosting), or a dedicated server. Players who join need neither
this plugin nor BepInEx. Editing a saved world with the game closed does not use it.


1. Install BepInEx (once)
-------------------------
  BepInExPack for Valheim: https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/
    - Your own game: with a mod manager (r2modman, Thunderstore Mod Manager), or by hand into the
      Valheim folder, as the page explains.
    - A Windows server: copy the pack's contents into the server folder (next to
      valheim_server.exe).
    - A Linux server: copy the pack's contents into the server folder and start the server with
      the pack's BepInEx start script instead of the usual one.
  Start the game or server once: BepInEx creates the folders BepInEx/plugins and BepInEx/config.


2. Install the plugin
---------------------
  1. Copy WorldEditorBridge.dll (in this folder) into BepInEx/plugins (of your Valheim, of your mod
     manager profile, or of the server).
  2. Restart the game or server. BepInEx/LogOutput.log then shows
     "WorldEditorBridge @VERSION@ listening on http://127.0.0.1:5182/".
  3. The plugin wrote BepInEx/config/local.worldeditorbridge.cfg with a random Token. Keep it
     secret: anyone with it and access to the port can change the world.

  Settings in that file:
    BindAddress = 127.0.0.1   only this computer can connect (a server is reached through a tunnel)
    Port = 5182               change it if something else already uses 5182
    Token = <random>          empty = a new one is made at the next start


3. Connect from the editor
--------------------------
  Your own game:  start page, "My game". Load your world in Valheim; the editor finds the plugin
                  and its token by itself and shows "Edit live".
  A server:       start page, "A dedicated server". Enter the address, your SSH user and password
                  (or key file), and the Token from the file above, then Connect. The editor makes
                  its own encrypted tunnel; the server is then saved for one-click access.
                  (Your own tunnel instead, with ssh -L 5182:127.0.0.1:5182 or PuTTY: "More options",
                  "I made my own tunnel".)

  If it does not connect, the editor says why:
    "refused the login"           check the SSH user and password or key.
    "No answer"                   check the server address, and that SSH is enabled on it.
    "refused the token"           copy the Token from the .cfg file again.
    "does not answer"             the game or server is not running with BepInEx and the plugin.
    "does not host the world"     the plugin runs in a game that joined someone else's server.
    "identity changed"            the server's SSH identity is not the one seen before: if you did
                                  not reinstall it, do not connect.
