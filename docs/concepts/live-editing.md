# Live editing

In live mode the editor works on the world of a **running** game instead of its files. Players can stay connected:
they see your changes as soon as you apply them, and you see what they build while you edit. This page explains how
the editor reaches the game, what travels between them, and what happens when players and the editor change the same
place.

## How it connects

Live mode needs a small plugin, **WorldEditorBridge**, inside the game that **hosts** the world: the dedicated server,
or your own game when you play alone or host. It runs on **BepInEx**, the usual Valheim mod loader. Players who join
need neither, console players included, and offline editing needs neither.

The plugin answers the editor on a port of the computer it runs on (5182 at first), and it is built to stay private:

- **It only listens on that computer itself** (`127.0.0.1`), so nothing on the network can reach it. Its
  `BindAddress` setting can open it to the network; keep it as it is.
- **It needs its secret token.** On its first start the plugin writes `BepInEx/config/Tie.WorldEditorBridge.cfg` with
  a **token** of 24 random letters and digits. A request without it is refused. Until the game or server has started
  once with the plugin, there is no token.

**Your own game.** The editor runs on the same computer, so it reads the port and the token from that file itself, in
every BepInEx folder it knows (your Valheim folder, your mod manager profiles, the folders added in **Settings**), and
asks each plugin found whether a world is loaded.

**A dedicated server.** The plugin cannot be reached from your computer directly, so the editor builds its own way in:

```text
 your computer                                   the server
 ┌──────────────────────┐   SSH, encrypted   ┌─────────────────────────────────────┐
 │ editor ── port 15182 ═╪═══════════════════╪═► 127.0.0.1:5182  WorldEditorBridge │
 └──────────────────────┘                    │                   inside Valheim    │
                                             └─────────────────────────────────────┘
```

1. It logs in to the server over **SSH** with your account (it has its own SSH, so no `ssh` program is needed).
2. It forwards a free port of your computer (from 15182 up) through that encrypted connection to the plugin's port on
   the server: an **SSH tunnel**.
3. It sends the token, which **you** type: the editor never reads it from the server. So an SSH login alone is not
   enough to change the world; whoever connects must also know the plugin's secret.

The editor also remembers the server's identity on the first connection. If it ever changes, the editor refuses to
connect, because someone could be pretending to be your server.

The plugin only changes a world its game hosts. In a game that joined someone else's server it refuses, since such a
game only knows the objects around its own player. Use the plugin from the same release as your editor.

## What is sent and when

**When you connect**, the editor reads the whole world from the game: every object, the zones the game has made, the
locations. It is the same data a save holds, so the map, the search and every tool work as offline. On a big world
this takes a while, up to a few minutes. It also asks for the objects the game knows that are not Valheim's own
(mods' pieces), so they get their names and show in the Place list and the Workshop.

**When you apply** (**Apply live**, or each step at once with **Auto**), it sends what changed since the last apply, in
this order:

1. **The ground** of every zone you changed, No limit ground included (see [No limit](ground-limits.md#no-limit)).
2. **The objects** you deleted, placed, pasted, replaced, changed or moved.
3. **Zone resets**, last. The game makes those zones again, so the editor then reads the world again and the history
   starts over.

The game shares each change with every player connected, as it does with what players build, and saves it with the
world. Objects placed live are made by the server, so players see them like something just built or planted.

The plugin does all this in the game's own loop, a few requests per frame, so the game never reads or changes its
world from two places at once. A big change (thousands of objects) can make the server hitch for a moment. If the game
does not answer within 30 seconds, nothing of that request is done and nothing is marked applied: apply again. One
apply runs at a time.

### Moving an object keeps it the same object

An object you move or turn moves in the game under its own id when nothing else about it changed. A mod that keeps
track of objects while the game runs (a chest's label sign, a warehouse's chests, a ward) then still knows it, and a
chest keeps its contents as the game has them. An object whose data you changed keeps its id the same way.

There is a catch. A player's game puts a standing object (a wall, a chest) in place once, as it comes into range, and
never moves it afterwards. So a move keeps the id only when no player has both its old and its new place loaded
(about 160 m round each player). Otherwise the object is made again at its new place and taken away at the old one,
and the apply message says how many: to other mods those are new objects. Move a build while nobody is near it to
keep everything about it. A mod's linked group (a ship and its pieces) and creatures are always made again.

## Following the game

While an area is open, the editor asks the game every 2 seconds what changed in that area's zones. This is cheap: the
game answers with one number per zone, which changes whenever something in the zone does, and only the zones whose
number changed are read again. The status bar says what came in ("From the game: 2 new object(s), 1 removed, the ground
of 1 zone(s)."); a change of data alone, such as a fire burning down, comes in without a word.

- **Objects** players build, remove or change appear, go or change in the editor, without a step in History. A changed
  object stays the same object in the editor, so the selection, the Inspector and its place in History stay with it.
- **Your own objects, once applied,** are shown as the game really has them: where physics settled a piece, the
  health and spawn point the game gave a creature.
- **Things that move or change on their own** (creatures, ships, carts, items lying about, fires and smelters) are
  followed as they go, without reading their whole zone each time.
- **Ground** dug, levelled or painted in game comes in where you have no changes of your own in that zone (see below
  when you do).

Following happens only in the 3D editor, never during a brush stroke or while applying. The map shows the world as it
was last read: **Reload** reads all of it again.

## Several editors and players

Players and the editor can change the same place. The editor settles it this way:

| What happens | What the editor does |
|---|---|
| Players change the ground of a zone where you have changes not applied yet | Yours are kept and the status bar says so. **Apply live** then asks first (**Apply over the game's changes**): applying writes your ground over that zone, and what players did to it since may be lost. **Not now** keeps your changes pending. |
| Players take or destroy an object | It is gone for good: undo and redo leave it alone, and a deletion of it you had not applied is dropped. That includes objects you placed. |
| You delete and apply an object | It stays deleted; undo brings it back as a copy (see [Applying live](saving-and-history.md#applying-live)). |
| You move a build while players are near it | It is made again rather than moved (see [Moving an object keeps it the same object](#moving-an-object-keeps-it-the-same-object)). |
| You change an object in the Inspector while the game changes it too | The Inspector does not apply over the newer version: it shows it, to change again. |
| You reset a zone a player stands near | The game makes it again at once; otherwise the next time someone comes. |

The plugin answers any editor that has the token, and it does not tell one editor's changes from a player's: what
another editor applies reaches yours the way a player's change does.

**Auto and Claude.** With **Auto** on, every step reaches the players the moment you finish it, a long brush stroke
going as one apply. For that reason Claude changes nothing in a live world while Auto is on (see
[Scripts and Claude](safety.md#scripts-and-claude)).
