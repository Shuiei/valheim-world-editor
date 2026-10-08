# Live mode

In live mode the editor works on the world of a **running** game instead of a save on disk.
Players can stay connected while you edit, and they see the changes as soon as you apply them. The
game then saves them like any other change.

It needs **BepInEx** (the mod loader, [BepInExPack for Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/)) and the **WorldEditorBridge** plugin
in the game that hosts the world: normally the dedicated server, or your own game when you play
single player or host. Players who join need neither, and offline mode does not use BepInEx.

## Your own game

For single player, or a world you host from the game.

1. **BepInEx in your Valheim**, once: install [BepInExPack for Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/)
   with a mod manager (r2modman, Thunderstore Mod Manager) or by hand into the Valheim folder, as
   its page explains. On Linux with the native game, BepInEx is started through the pack's start
   script (its page shows the Steam launch option); a mod manager does that for you.
2. **The plugin:** copy `plugin/WorldEditorBridge.dll` (in the editor's download) into
   `BepInEx/plugins` of your Valheim, or of your mod manager profile.
3. **Start Valheim** with BepInEx and load your world.
4. Start page → **My game**. It shows what is missing, or "Valheim is running with the world …"
   with an **Edit live** button. On your own computer the editor reads the plugin's settings (port
   and token) itself, in the Valheim folder and in r2modman / Thunderstore Mod Manager profiles.
   (For a server the token is always typed; see below.)

If your Valheim or your mod manager profile is not in the usual place, add it in **⚙ Settings** on
the start page (Valheim game folder, BepInEx folders).

The plugin listens on port 5182 of your computer. If something else uses that port (for example an
`ssh -L 5182:…` tunnel you opened by hand), it cannot start: close that tunnel, or change `Port` in
the plugin's settings (below).

## A dedicated server

1. **On the server, once:** BepInEx (BepInExPack for Valheim, following its dedicated-server
   instructions), then `plugin/WorldEditorBridge.dll` in its `BepInEx/plugins/`, then restart the
   server. Its log shows `WorldEditorBridge <version> listening on http://127.0.0.1:5182/`.
2. Start page → **A dedicated server**: the server's address, the user you log in to it with over
   SSH, the password or an SSH key file (without either, the usual keys in `~/.ssh` are tried), and
   the **plugin token** (the `Token` line of `BepInEx/config/Tie.WorldEditorBridge.cfg` on the
   server). The token is always typed by you: the editor never reads it from the server, so an SSH
   login alone is not enough to change the world. Tick **Save password** to keep the password (see
   [Saved servers](#saved-servers)).
3. **Connect.** The editor:
   - logs in over SSH (no `ssh` program needed, it has its own);
   - forwards a free port of your computer to the plugin through that encrypted connection;
   - checks that the plugin answers and accepts the token, then loads the world.
4. The server is saved in the list, with its token, for one-click access next time.

**Server's Valheim folder** (optional, on the form): the server folder that holds `BepInEx`. With
it, the editor reads the plugin's port from `BepInEx/config/Tie.WorldEditorBridge.cfg` there (never
the token), and when the plugin does not answer it checks that folder and says what is missing: the
folder, BepInEx, the plugin in `BepInEx/plugins`, or a server not started with BepInEx.

**More options** on the form: the key's passphrase, the plugin's port (when it
is not 5182 and no folder is given), and **I made my own tunnel** (address + token) for a tunnel made
with `ssh -L` or PuTTY.

A server is saved only after a successful connection. **edit** next to a saved server fills the
form with it, to change its folder, port or name.

### Saved servers

After the first successful connection the server is written to `servers.cfg` in the editor's data
folder (`~/.local/share/ValheimWorldEditor` on Linux, `%LOCALAPPDATA%\ValheimWorldEditor` on
Windows), one section per server, so it can also be edited by hand:

```
[My server]
Host = my.server.com
SshPort = 22
User = valheim
Password =
KeyFile =
Token = …
BridgePort = 5182
GameFolder = /home/valheim/server
HostKey = SHA256:…
```

- `Password` is only filled when **Save password** was ticked. It is stored as plain text: on Linux
  the file can only be read by you; prefer an SSH key where you can.
- `HostKey` is the server's identity, remembered on the first connection (trust on first use). If
  it ever changes the editor refuses to connect, because someone could be pretending to be your
  server. If you reinstalled the server, empty that line to accept the new identity.
- **forget** next to a saved server removes it.

### Plugin settings (`Tie.WorldEditorBridge.cfg`)

In `BepInEx/config`. Plugins older than 0.41.0 named it `local.worldeditorbridge.cfg`; the plugin
moves it to the new name on its first start (port and token are kept), and the editor reads either.

| Setting | Default | Meaning |
|---|---|---|
| `BindAddress` | `127.0.0.1` | Address the bridge listens on. Keep `127.0.0.1` and use an SSH tunnel; anything else exposes the bridge to the network. |
| `Port` | `5182` | TCP port of the bridge. |
| `Token` | generated | Secret the editor must send. Generated on first start when empty. Anyone with the token and access to the port can change the world, so keep it private. For your own game the editor reads it from this file by itself; for a server you type it in. |

## What changes in the editor

| Control | What it does |
|---|---|
| **LIVE** badge | Shows the editor is connected to the running game. |
| **Apply live** | Sends every pending change to the game: ground (height and paint), deleted objects, new objects (planted, pasted, replaced, moved), and zone resets. After a zone reset the world is read again from the game and the page reloads (the history starts over). |
| **Auto** | Applies after every stroke, placement and undo, without pressing Apply live. |
| **Reload** | Loads the world again from the game, to pick up what players changed since. Changes you have not applied are dropped (you are asked first). |
| Player names | Connected players are drawn at their position, with their name, and follow them. |

The pending counter reads "Not applied: …" and the History panel marks applied changes.

## Undo after applying

Applied changes can still be undone: undo (or Remove in History) and apply again. A deleted object
comes back as a copy of what it was when the snapshot was taken; a placed object is removed from
the game again.

## Limits

- **Zone resets** need WorldEditorBridge 0.10.0 or newer on the server. The game generates a reset
  zone again at once when a player is near it, otherwise the next time someone comes. A reset cannot
  be undone after it is applied.
- The plugin only works in a game that **hosts** the world (server, single player or host). In a
  game that joined someone else's server it refuses to change anything.
- Objects placed live are created by the server; players see them like objects someone just
  built or planted.
- Big changes (thousands of objects at once) are applied in one go and can make the server hitch
  for a moment.
