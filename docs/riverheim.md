# Riverheim

[Riverheim](https://thunderstore.io/c/valheim/p/Gurebu/Riverheim/) (by Gurebu) replaces the game's terrain and
biome generation: larger continents, forking rivers, mountains and lakes. A world made with it can only be drawn
by Riverheim itself: the game without the mod does not open it (it shows `[BAD_VERSION]`), and neither does the
editor without it.

## What the editor does with a Riverheim world

- The worlds list tags it **Riverheim** on its card.
- Opening it needs the **Riverheim** switch in Settings on and Riverheim installed in a BepInEx folder the editor
  knows (the Valheim folder, the editor's own profile, the folders added in Settings, r2modman's and Thunderstore Mod
  Manager's profiles, or the Expand World folder). The editor draws the world with your own copy of Riverheim: it
  carries none of the mod.
- When it cannot be opened, the card says why: the switch is off, Riverheim is not installed anywhere the editor
  looks, the world was made with Riverheim's deprecated beta (no Riverheim release opens those), or the world file
  has a Riverheim setting this editor does not know.

This version recognises Riverheim worlds and says why it does not open them yet; drawing them comes next.

Worlds made without Riverheim never use it, even with the switch on and the mod installed.
