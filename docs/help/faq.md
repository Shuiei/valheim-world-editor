# Questions

Short answers to what people ask most, each with a link to the full one.

## Is it safe for my world?

Yes, if you keep a copy. Nothing is written until you press **Save to world** or **Apply live**, and every save is
read back and checked: if it does not read back right, the new files are removed and the world is as it was. Still,
copy a world you care about before your first save: see [Back up a world before editing](../guides/back-up-a-world.md)
and [What the editor writes](../concepts/safety.md#what-the-editor-writes).

## Can I undo after saving?

Yes. The history is kept with the world after each save or apply, even after you close the editor: undo a step (or
**Remove** it in History) and save again. See [Undo a step, take back one change, or discard everything](../guides/undo-changes.md).
For damage found later, [Restore one area from a backup](../guides/restore-from-backup.md).

## Do the players on my server need anything?

No. Only the game or server that hosts the world needs BepInEx and the plugin, and only for live editing. Players
who join need nothing, and that includes ground raised past the game's 8 m limit. See
[Ground limits](../concepts/ground-limits.md#no-limit).

## Can console players on crossplay join an edited world?

Yes: an edited world is still a normal Valheim world, and the editor's ground past 8 m needs no mod. The exceptions
are worlds that need a mod themselves: one made with Riverheim, or a server running Homestead, turns away players
without the mod, and console players cannot install mods. See [A new world](../new-world.md#tips-and-limits).

## Can I edit while the game is running?

Yes, in live mode, with the WorldEditorBridge plugin in the game or server: see
[Edit the world you are playing](../guides/edit-your-game-live.md) or
[Edit a dedicated server while people play](../guides/edit-a-server-live.md). To edit a saved world's files, close
the game or stop the server first: a running game saves over what the editor writes. See
[The game saved while you were editing](troubleshooting.md#the-game-saved-while-you-were-editing).

## The editor asks for a token. Where is it?

In `BepInEx/config/Tie.WorldEditorBridge.cfg` of the game or server, on the `Token` line. The plugin writes that file
the first time the game starts with it. For your own game the editor reads it by itself; for a server you copy it
from the server. See [Plugin settings](../live-mode.md#plugin-settings).

## Does it work with mods?

Yes. In a live modded game the editor learns the mods' objects and draws their models (see
[Modded games](../live-mode.md#modded-games)). Mods that change how worlds are made need the editor's add-on for them:
Expand World, Riverheim and Tunnelheim have one. See
[Edit a world made with Expand World, Riverheim or Tunnelheim](../guides/use-mods-worlds.md).

## I play with the Expand World mods. Does the editor know?

Yes, with the Expand World add-on installed: the ground, the map, the biomes and the predictions follow the mods'
settings and data files. Without it, every world is generated as the game itself does. See
[Edit a world made with Expand World, Riverheim or Tunnelheim](../guides/use-mods-worlds.md).

## My world was made with Riverheim. Can I open it?

Yes, with the Riverheim add-on installed and Riverheim itself in a BepInEx folder the editor knows: the editor draws
the world with your own copy of the mod. See [Edit a world made with Expand World, Riverheim or Tunnelheim](../guides/use-mods-worlds.md).

## Do I need Homestead?

Only to build the editor's blueprints in game. The Workshop, the library and pasting blueprints into a world work
without it; when it is missing, the start page says so. Note that a server running Homestead turns away players who
do not have it. See [The Workshop](../workshop.md).

## Where do I download it? Is it on Thunderstore, Hexium or Nexus Mods?

- **The editor** is a program, not a mod: download it from the
  [releases page](https://github.com/Shuiei/valheim-world-editor/releases/latest) or from
  [Nexus Mods](https://www.nexusmods.com/valheim/mods/4348).
- **The WorldEditorBridge plugin** (for live editing) is on the releases page, Nexus Mods,
  [Thunderstore](https://thunderstore.io/c/valheim/p/Tie/WorldEditorBridge/) and
  [Hexium](https://valheim.hexium.gg/mods/Tie/WorldEditorBridge), for mod managers.
- **Add-ons** for mods are on the releases page and Nexus Mods.

Use the plugin and add-ons of the same release as the editor. See [Install](../start/install.md).

## Does it send anything over the internet?

No. Everything happens on your computer; a dedicated server is reached only through the editor's own encrypted SSH
tunnel, with a token you type. See [Nothing leaves your computer](../concepts/safety.md#nothing-leaves-your-computer).

## Is it safe to let Claude or a script change my world?

What Claude changes stays pending until you save, and it can only connect from this computer with a token. Scripts
are programs that run with your rights, so Claude may run them only if you allow it. See
[Scripts and Claude](../concepts/safety.md#scripts-and-claude).

## Something does not work. Where do I look?

Start with [Troubleshooting](troubleshooting.md). To report a bug, attach the editor's log:
[The log and bug reports](troubleshooting.md#the-log-and-bug-reports).
