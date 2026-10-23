# Valheim World Editor

[![CircleCI](https://dl.circleci.com/status-badge/img/gh/Shuiei/valheim-world-editor/tree/main.svg?style=svg)](https://dl.circleci.com/status-badge/redirect/gh/Shuiei/valheim-world-editor/tree/main)
[![Codecov](https://codecov.io/gh/Shuiei/valheim-world-editor/graph/badge.svg)](https://codecov.io/gh/Shuiei/valheim-world-editor)
[![Latest release](https://img.shields.io/github/v/release/Shuiei/valheim-world-editor)](https://github.com/Shuiei/valheim-world-editor/releases/latest)
[![OpenSSF Scorecard](https://api.scorecard.dev/projects/github.com/Shuiei/valheim-world-editor/badge)](https://scorecard.dev/viewer/?uri=github.com/Shuiei/valheim-world-editor)
[![OpenSSF Best Practices](https://www.bestpractices.dev/projects/15376/badge)](https://www.bestpractices.dev/projects/15376)

**A world editor for Valheim, in the spirit of Minecraft's MCEdit, WorldEdit and WorldPainter:** open a world, saved
or running, in a 3D window drawn with the game's own terrain, textures and models; shape it, build, and save it back
or watch it change in game as you work.

![The 3D editor showing a base, with the tool rail, tool panel and View panel](docs/images/overview.jpg)

## Download

**[Download the latest release](https://github.com/Shuiei/valheim-world-editor/releases/latest)** for Windows or
Linux, or macOS on Apple silicon (experimental). Unpack it and start it: nothing else to install.

Also on [Nexus Mods](https://www.nexusmods.com/valheim/mods/4348) (the editor and its plugin). The WorldEditorBridge
plugin, for live editing and mod managers, is on [Thunderstore](https://thunderstore.io/c/valheim/p/Tie/WorldEditorBridge/)
and [Hexium](https://valheim.hexium.gg/mods/Tie/WorldEditorBridge).

## What you can do

- **Sculpt and paint the ground** with brushes that raise, lower, flatten, smooth, erode, naturalize or restore it,
  and paint dirt, cultivated soil or paved stone.
- **Go past the game's ±8 m limit**: raise mountains, ridges and volcanoes or dig canyons. Players need no mod,
  console players included.
- **Draw roads, ramps, rivers and caves** along a line.
- **Place anything the game has**, trees, rocks, crops and building pieces, with a brush, lines, grids or whole
  zones; select, move, turn, copy and delete objects, whole buildings included.
- **Look inside objects and change them**: chest contents, sign texts, portal tags. Search the whole world for an
  object, an item in any chest, or a sign's text.
- **Build in the Workshop** with every piece of the hammer, cultivator and serving tray, with the game's snapping and
  support check, and keep it as a [Homestead](https://thunderstore.io/c/valheim/p/sighsorry/Homestead/) blueprint to
  build in game or paste into any world.
- **Generate a whole dungeon**, or edit Frost Caves, crypts and other dungeons room by room.
- **See a seed's world before it exists**, find a seed with what you want near the start, and make the world.
- **Edit live**: your own game or a dedicated server while people play, and see each change in game at once.
- **Undo anything**, take out one change from the middle, or restore an area from a backup to undo griefing.

## Quick start

1. [Install the editor](docs/start/install.md): download, unpack, start it.
2. [Back up a world](docs/guides/back-up-a-world.md), then follow [your first edit](docs/start/first-edit.md):
   raise a hill, plant trees, save, and see it in game.
3. Pick a way to edit on the start page: **My game** or **A dedicated server** (live), or **A saved world** with the
   game closed. In the editor, `F1` opens the help at the tool you are using.

## Documentation

The same documentation is in the editor (**Documentation** on the start page): start at
[docs/README.md](docs/README.md).

- **Getting started**: [Install](docs/start/install.md), [your first edit](docs/start/first-edit.md),
  [your first build](docs/start/first-build.md).
- **How-to guides**: one task each, from [editing your game live](docs/guides/edit-your-game-live.md) to
  [restoring an area from a backup](docs/guides/restore-from-backup.md). The list is in [docs/README.md](docs/README.md).
- **Tools**: [the editor window](docs/editor-basics.md), [the world map](docs/map.md),
  [sculpt and paint](docs/terrain.md), [Place](docs/place.md), [Select](docs/select.md),
  [the Workshop](docs/workshop.md), [live mode](docs/live-mode.md) and the rest.
- **How it works**: [saving and history](docs/concepts/saving-and-history.md),
  [ground limits](docs/concepts/ground-limits.md), [safety](docs/concepts/safety.md).
- **Help**: [troubleshooting](docs/help/troubleshooting.md), [questions](docs/help/faq.md),
  [shortcuts](docs/help/shortcuts.md), [glossary](docs/help/glossary.md).
- **Mods**: [Expand World](addons/ExpandWorld/README.md), [Riverheim](addons/Riverheim/README.md),
  [Tunnelheim](addons/Tunnelheim/README.md) and [ValheimRAFT](addons/ValheimRAFT/README.md) add-ons;
  [for mod authors](docs/modding.md).

What changed and when: [CHANGELOG.md](CHANGELOG.md).

## Safety

Nothing is written until you press **Save to world** or **Apply live** (or, live with **Auto** on, as you make each
change), and every offline save is read back and checked before the old one goes.

The plugin only listens on the computer it runs on and needs a secret token. Everything happens on your computer and
nothing is sent anywhere else, except what you let Claude see when you connect it (the area's data and pictures of the
view go to your Claude app).

Keep your own copy of a world you care about anyway: see
[Back up a world before editing](docs/guides/back-up-a-world.md). The details: [Safety](docs/concepts/safety.md).
Found a security problem? Report it privately, see [SECURITY.md](SECURITY.md).

## For developers

The program also opens a world or a live game from the command line:

```sh
ValheimWorldEditor "<world folder>"                                   # open that world's map
ValheimWorldEditor --live http://127.0.0.1:5182 --token <token>       # connect to a game (or WORLD_BRIDGE_TOKEN)
ValheimWorldEditor --world "<world folder or name>" --zone 3,-2       # straight into the 3D editor there
```

Build it from source with the .NET 10 SDK:

```sh
dotnet publish editor/Desktop/ValheimWorldEditor.Desktop.csproj -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true -o ../ValheimWorldEditor
```

Use `-r win-x64` for Windows. `tools/release.sh <folder>` builds the complete release packages (the program and its
README; the Mac app signed ad hoc with [rcodesign](https://github.com/indygreg/apple-platform-rs), which `NO_MACOS=1`
leaves out); `tools/thunderstore.sh <folder>` the plugin's (`WorldEditorBridge-<version>.zip`). CircleCI runs the
tests on every push (the app's visual tests need a display; without one they skip, or leave them out with
`--filter-not-trait Category=Visual`):

```sh
dotnet test --project editor/tests/Core.Tests/Core.Tests.csproj -c Release
dotnet test --project editor/tests/Desktop.Tests/Desktop.Tests.csproj -c Release
```

Project layout, the documentation's pictures and the tests: [docs/development.md](docs/development.md). How the code
fits together: [docs/architecture.md](docs/architecture.md). Translating: [docs/translating.md](docs/translating.md).

## License

[MIT](LICENSE), © 2026 Thibault Bene. The repository holds none of Valheim's code: the world generator is written for
the editor, following the game's formulas. The data files read from the game remain Iron Gate's and are not covered:
see [NOTICE.md](NOTICE.md). Not affiliated with or endorsed by Iron Gate or Coffee Stain.
