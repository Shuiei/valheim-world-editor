# Changelog

The editor's own changes: [CHANGELOG.md](https://github.com/Shuiei/valheim-world-editor/blob/main/CHANGELOG.md).

## 0.3.0

First release on Thunderstore (works with Valheim World Editor 0.2.0 and later).

- **Objects live**: deleted, planted, pasted and moved objects are applied in the running world,
  and undoing after applying takes them back in the game too.
- **Ground live**: terrain edits (sculpt, paint, roads, areas) are sent to the game and shown to
  every player at once.
- **World snapshot**: the editor opens the running world as the game has it now, without a save.
- Listens on `127.0.0.1:5182` only, with a random token in `BepInEx/config/local.worldeditorbridge.cfg`.
