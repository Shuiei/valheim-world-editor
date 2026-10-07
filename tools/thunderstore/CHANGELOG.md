# Changelog

The editor's own changes: [CHANGELOG.md](https://github.com/Shuiei/valheim-world-editor/blob/main/CHANGELOG.md).

## 0.20.0

- Editor changes only (fences end to end). The plugin is unchanged.

## 0.19.2

- Editor changes only (moved objects on the ground). The plugin is unchanged.

## 0.19.1

- Editor changes only (area selection). The plugin is unchanged.

## 0.19.0

- Editor changes only (selection helpers). The plugin is unchanged.

## 0.18.0

- Editor changes only (select a whole building). The plugin is unchanged.

## 0.17.0

- Editor changes only (river and canal). The plugin is unchanged.

## 0.16.0

- Editor changes only (shape tool). The plugin is unchanged.

## 0.15.0

- Editor changes only (erosion). The plugin is unchanged.

## 0.14.0

- Editor changes only (heightmaps). The plugin is unchanged.

## 0.13.0

- Editor changes only (stamps). The plugin is unchanged.

## 0.12.0

- Editor changes only (brush shapes). The plugin is unchanged.

## 0.11.0

- Editor changes only (restore from a backup). The plugin is unchanged.

## 0.10.0

- New: `POST /zones/reset`, so the editor can reset zones in the running game (they are generated
  again at once where players are). Players' tombstones are always kept.

## 0.9.0

- Editor changes only (search the world). The plugin is unchanged.

## 0.8.0

- Editor changes only (object inspector). The plugin is unchanged.

## 0.7.0

- Editor changes only (repeat a paste). The plugin is unchanged.

## 0.6.0

- Editor changes only (PlanBuild files). The plugin is unchanged.

## 0.5.0

- Editor changes only (blueprints). The plugin is unchanged.

## 0.4.1

- The editor is downloaded from the
  [GitHub releases page](https://github.com/Shuiei/valheim-world-editor/releases/latest) (this page
  now says so): Thunderstore carries only this plugin. The plugin is unchanged.

## 0.4.0

- Fixed: leaving the world map with **Worlds** while it was still loading could raise an error.
- Unused code removed. The plugin is unchanged.

## 0.3.3

- The editor packages depend on
  [WorldEditorBridge](https://thunderstore.io/c/valheim/p/Tie/WorldEditorBridge/) instead of
  carrying their own copy of it: mod managers install it with them. The plugin is unchanged.

## 0.3.2

- Smaller: the editor's bundled Python keeps only what it uses (Windows: 74 → 48 MB, Linux:
  117 → 87 MB unpacked), and the packages drop the `README.txt` files this page replaces.
- Windows: carries `msvcp140.dll`, needed to copy the game's look on a PC without the Visual C++
  runtime. The plugin is unchanged.

## 0.3.1

- **Three packages**: `ValheimWorldEditor_Windows` and `ValheimWorldEditor_Linux` (the editor with
  the plugin) and `WorldEditorBridge` (the plugin alone, for servers). The plugin is unchanged.
- The editor finds the mod manager profile it is installed in, and on Linux makes its bundled
  Python runnable again after a mod manager unpacked it.

## 0.3.0

First release on Thunderstore. From this version on, the plugin has the same version number as
Valheim World Editor: use the two together.

- **Objects live**: deleted, planted, pasted and moved objects are applied in the running world,
  and undoing after applying takes them back in the game too.
- **Ground live**: terrain edits (sculpt, paint, roads, areas) are sent to the game and shown to
  every player at once.
- **World snapshot**: the editor opens the running world as the game has it now, without a save.
- Listens on `127.0.0.1:5182` only, with a random token in `BepInEx/config/local.worldeditorbridge.cfg`.
