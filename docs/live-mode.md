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
2. **The plugin:** install WorldEditorBridge with a mod manager (Thunderstore or Hexium), or copy
   `WorldEditorBridge.dll` from `WorldEditorBridge-<version>.zip` (on the editor's releases page)
   into `BepInEx/plugins` of your Valheim, or of your mod manager profile.
3. **Start Valheim** with BepInEx and load your world. The plugin's first start writes its
   settings (`BepInEx/config/Tie.WorldEditorBridge.cfg`, with the token) that the editor reads.
4. The start page notices the game by itself: **Home** shows "Valheim is running with the world …"
   with an **Edit live** button, and **My game** in its menu says *running* (before that, **My
   game** shows what is missing). On your own computer the editor reads the plugin's settings (port
   and token) itself, in the Valheim folder and in r2modman / Thunderstore Mod Manager profiles.
   (For a server the token is always typed; see below.)

If your Valheim or your mod manager profile is not in the usual place, add it in **Settings** on
the start page (Valheim game folder, BepInEx folders).

The plugin listens on port 5182 of your computer. If something else uses that port (for example an
`ssh -L 5182:…` tunnel you opened by hand), it cannot start: close that tunnel, or change `Port` in
the plugin's settings (below).

## A dedicated server

1. **On the server, once:** BepInEx (BepInExPack for Valheim, following its dedicated-server
   instructions), then `WorldEditorBridge.dll` (from `WorldEditorBridge-<version>.zip`) in its
   `BepInEx/plugins/`, then restart the
   server. Its log shows `WorldEditorBridge <version> listening on http://127.0.0.1:5182/`. This
   first start with the plugin writes `BepInEx/config/Tie.WorldEditorBridge.cfg` and its token: until
   the server has started once with the plugin, there is no token to enter.
2. Start page → **A dedicated server** (under **Edit a world**): the server's address, the user you log in to it with over
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
form with it, to change its folder, port or name. A saved server with a key file has a **Key
passphrase** box next to it: the passphrase is never saved, so type it there when the key has one.
Of the usual keys in `~/.ssh`, one the editor cannot use (a passphrase, a format it does not read) is
skipped and the others and the password are still tried.

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
  server; this holds for every account on that address and port, not only the one saved. If you
  reinstalled the server, empty that line (in each saved server with that address) to accept the new
  identity.
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
| **Apply live** | Sends every pending change to the game: ground (height and paint), deleted objects, new objects (planted, pasted, replaced, moved), and zone resets. After a zone reset the world is read again from the game (the history starts over). |
| **Auto** | Applies after every stroke, placement and undo, without pressing Apply live. Turning it on sends what is already waiting. Remembered. |
| **Reload** | Loads the whole world again from the game. Changes you have not applied are dropped (you are asked first). You stay in the same place. The open area follows the game by itself (below): Reload is for the rest of the world, or an older plugin. |
| Players | Connected players are drawn at their position with their name, in the 3D view (blue posts) and on the map, and follow them. **View → Players → Go to** takes you to one. The map says how many are online. |

The pending counter reads "Not applied: …" and the History panel marks applied changes.

## Following the game

While an area is open, the editor asks the game every 2 seconds what changed in that area's zones
(only those zones, not the whole world: the game answers with one number per zone, and only the
zones whose number changed are read again).

- **Objects** players built, removed or changed there appear, go or change in the editor, without
  a step in History. A changed object (a chest used, a piece moved) stays the same object in the
  editor, so its place in History, the selection and the inspector stay with it; the inspector does
  not apply over a version the game changed meanwhile (it shows the new one to change again). An
  object you deleted and applied stays deleted (undo brings it back); one the game removed is gone
  for good: undo and redo leave it alone, and a deletion of it not applied yet is dropped. Objects
  you placed that players then picked or destroyed go too. Creatures and other things that move by
  themselves, and fires, smelters, beehives and the like (which change their own data all the time),
  are followed when they appear or go, not for every move or tick; portals are followed too.
- **Ground** dug, flattened, paved or cultivated in the game comes in where you have no changes of
  your own in that zone. Where you have changes not applied yet, yours are kept and the status bar
  says so. **Apply live** (and **Auto**) then asks first: applying writes your ground over that zone,
  and what players did to its ground since may be erased. Answer **Not now** to keep your changes
  pending (nothing is applied then); undo them and the game's ground comes in at the next look.
- Following happens only in the 3D editor, never during a brush stroke or while applying; the map
  shows the world as it was last followed (**Reload** reads all of it again).

The status bar says what came from the game ("From the game: 2 object(s) made or changed…").
Following needs this version of the WorldEditorBridge plugin on the server; with an older one the
editor says so, and **Reload** reads the world again.

## Modded games

Mods make most of their objects while the game runs, so the editor cannot find them in Valheim's
files. When a live world opens, the editor asks the game for every object it knows and keeps those
that are not Valheim's own: a modded game's objects then have their names, the Place list offers
them, the Workshop's Build panel lists the mods' building pieces on a **Mods** tab with their cost
(whatever tool builds them in game: a mod's own hammer too), and the inspector says which mod an
object comes from (with Jötunn installed, which knows; else "From a mod").

What the editor learns is kept (`mod-catalogue.json` in the editor's data folder), so a modded
server's world opened afterwards from its files, offline, shows them too. Each connection adds to it;
a mod's newer pieces replace their older entries. Needs WorldEditorBridge 1.26.0 or newer; with an
older one the world opens as before.

Their models are drawn as in game (WorldEditorBridge 1.27.0 or newer): the game tells what it draws
of each mod object, and the editor finds those meshes, materials and textures in the mods' own
files, which it reads from your Valheim folder and your mod manager profiles (the same mods must be
installed there, as players need them anyway), else in Valheim's. The first time, the mods' bundles
are copied out of their DLLs into the editor's data folder (`mod-bundles`, a few seconds; again only
when a mod changes). Meshes a mod builds in code while the game runs (ValheimRAFT's sails, for
example) cannot be found and are not drawn: those pieces show untextured.

A mod can also ship a `worldeditor.json` for what the editor cannot guess (its data's labels, helpers
not to offer, groups for its pieces): see [For mod authors](modding.md).

The Workshop's **Mods** tab lists one mod's pieces at a time: choose the mod in the row under the
tabs (each with its number of pieces). At most 300 pieces show at once; search to find the others.

### Mods' own plugins

A mod whose plugin registered with the bridge (its author did, through the bridge's API: see
[Live mode for mods](modding.md#live-mode)) is told what the editor applied, right away, so what it
builds out of its objects while the game runs (a ship from its pieces) is put together again at
once; and its editor extension can ask that plugin for what only the running game knows. The
editor's log (Settings → Open log) names the mods registered when a game is opened.

## Moving things

An object you move (the Select tool's move or turn, or a **Cut** then pasted) moves in the game as
the same object, under its own id, when nothing about it changed but its place and turn. A mod that
keeps track of objects while the game runs (a chest's label sign, a warehouse's chests, a ward)
then still knows it, instead of seeing an object gone and a new one made, and the chest keeps what
is in it as the game has it. Undo moves it back the same way.

A player's game puts a standing object (a wall, a chest, a sign) where it is once, as it comes
into range, and does not move it afterwards. So an object is moved this way only when no player
has both its old and its new place loaded (about 160 m around each player, more for the large
objects seen from afar). Otherwise, as before, it is made again at its new place and taken away
where it was, and the message after **Apply live** says how many: those are new objects to the
other mods. Move a build while nobody is near it to keep everything about it.

Objects that change on the way (a copy whose data you edited, the parts of a mod's linked group
such as a ship, creatures) are always made again. A plugin older than 1.40.0 makes every moved
object again.

## Undo after applying

Applied changes can still be undone: undo (or Remove in History) and apply again. A deleted object
comes back as a copy of what it was when the snapshot was taken; a placed object is removed from
the game again.

## What the plugin logs

The server's (or game's) `BepInEx/LogOutput.log` says what each apply did, for when a change does not
show for every player:

- `WorldEditorBridge: destroyed N object(s) (M found again at their place under a new id, K already
  gone), created C, moved I in place and F by making it again (a player had both places loaded, or it
  was gone); A -> B object(s) known, P peer(s)` — one line per apply of objects: how many objects the
  game knew before and after it (the destroyed ones go on the game's next update, so B still counts
  them) and how many players are connected. The moves part is there when the apply moved objects
  ([Moving things](#moving-things)).
- `WorldEditorBridge: 1 s after the apply: S of N destroyed object(s) still exist, R other object(s)
  of their prefab within 10 cm of their place (made again, or there all along); B object(s), P
  peer(s)` — a second later. A destroyed object the game still has means something on the server
  made it again or kept it (a mod, most likely). R counts other objects of the same prefab standing
  within 10 cm of where a destroyed one stood (those the same apply created left out): one made again
  by a mod, or one that was there all along, which the line cannot tell apart — compare with what
  stood there. Both at zero means the server did its part, and what a player still sees is that
  player's own game.
- `WorldEditorBridge: applied terrain to N zone(s) (C new)` and `WorldEditorBridge: reset N zone(s):
  …` for the ground and for zone resets.

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
