# Notice

Valheim World Editor and WorldEditorBridge are under the [MIT License](LICENSE), Copyright (c) 2026
Shuiei, except for the parts below.

## Valheim's own code and data

Valheim is made by Iron Gate AB and published by Coffee Stain. This project is not affiliated with
or endorsed by them. These files come from the game and remain theirs; the MIT License does not
cover them:

- `Core/WorldGen/WorldGenerator.cs` and `Core/WorldGen/DUtils.cs`: decompiled from the game, so
  the editor generates terrain exactly like it.
- The data read from the game's files in `Core/WorldGen/`: `prefabs.json`, `pieces.json`,
  `piece-cost.json`, `piece-place.json.gz`, `piece-support.json`, `vegetation.json`,
  `cave-rocks.json`, `terrain-modifiers.json`, `zdo-keys.json` and `dungeon-rooms.json.gz`.

The game's textures and models are not in this repository or in the releases: the editor copies
them from the player's own Valheim install, on their computer.

## Third-party code

- `Core/WorldGen/FastNoise.cs`: FastNoise, MIT License, Copyright (c) 2017 Jordan Peck (its notice
  is kept in the file).
- `tools/asset-export/smolv.py`: a Python port of the decoder in [SMOL-V](https://github.com/aras-p/smol-v),
  MIT License, Copyright (c) 2016-2024 Aras Pranckevicius.
- [SPIRV-Cross](https://github.com/KhronosGroup/SPIRV-Cross) (Apache License 2.0), through Silk.NET's
  native build of it, turns the terrain shader from Valheim for Windows into GLSL.
- Libraries the editor is built with, each under its own license: [Avalonia](https://github.com/AvaloniaUI/Avalonia)
  (MIT), [Silk.NET](https://github.com/dotnet/Silk.NET) (MIT), [SSH.NET](https://github.com/sshnet/SSH.NET)
  (MIT), [Roslyn](https://github.com/dotnet/roslyn) (MIT), for scripts, and, to read the game's models
  and textures from its own files, [AssetsTools.NET](https://github.com/nesrak1/AssetsTools.NET) (MIT)
  and [BCnEncoder.NET](https://github.com/Nominom/BCnEncoder.NET) (MIT or Unlicense).
- The game-look exporter in the release packages runs on a bundled [Python](https://www.python.org/)
  (Python Software Foundation License) with [UnityPy](https://github.com/K0lb3/UnityPy) (MIT) and the packages it needs, each under its own
  license.
