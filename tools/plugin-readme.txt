WorldEditorBridge 0.3.0: the server plugin for live mode (Valheim World Editor @VERSION@)
==========================================================================================

WorldEditorBridge lets the editor change a world while the server runs: players stay connected
and see the changes as soon as you apply them. It is only needed for live mode; editing a world
saved on disk does not use it. Players who join the server need neither this plugin nor BepInEx.


1. Install BepInEx on the server (once)
---------------------------------------
  Download BepInExPack for Valheim: https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/
  and follow its instructions for a dedicated server:
    - Windows server: copy its contents into the server folder (next to valheim_server.exe).
    - Linux server: copy its contents into the server folder and start the server with the
      BepInEx start script that comes with the pack (instead of the usual start script).
  Start the server once: BepInEx creates the folders BepInEx/plugins and BepInEx/config.


2. Install the plugin
---------------------
  1. Copy WorldEditorBridge.dll (in this folder) into the server's BepInEx/plugins/ folder.
  2. Restart the server. Its log (BepInEx/LogOutput.log) then shows "WorldEditorBridge" loading.
  3. The plugin wrote BepInEx/config/local.worldeditorbridge.cfg with a random Token. You need
     this token in the editor. Keep it secret: anyone with it and access to the port can change
     the world.

  Settings in that file:
    BindAddress = 127.0.0.1   (only the server itself can connect: keep it, and use a tunnel)
    Port = 5182
    Token = <random>          (empty = a new one is made at the next start)


3. Connect from the editor
--------------------------
  1. On your computer, open an SSH tunnel to the server and keep it running while you edit:
         ssh -N -L 5182:127.0.0.1:5182 user@your-server
     (Windows 10/11: in PowerShell or the Command Prompt. Linux: in a terminal.)
     Windows: if "ssh is not recognized", turn it on in Settings > System > Optional features >
     Add a feature > OpenSSH Client. With PuTTY: Connection > SSH > Tunnels, Source port 5182,
     Destination 127.0.0.1:5182, Add, then open the session.
     When the game runs on your own computer (single player, or host and play), skip this:
     connect to 127.0.0.1:5182 directly.
  2. In the editor's start page, under "A running server (live)": 127.0.0.1:5182, the Token,
     Connect.

  If it does not connect, the editor says why:
    "No answer" / "Nothing answers"  the tunnel is not open, or the server is not running with
                                      BepInEx and the plugin.
    "refused the token"               copy the Token from the .cfg file again.
    "does not host the world"         the plugin runs in a game that joined another server.
