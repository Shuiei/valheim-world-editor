# Notice

Valheim World Editor and WorldEditorBridge are under the [MIT License](LICENSE), Copyright (c) 2026
Thibault Bene, except for the parts below.

## Valheim's data

Valheim is made by Iron Gate AB and published by Coffee Stain. This project is not affiliated with
or endorsed by them. The repository holds none of the game's code: the world generator
(`editor/Core/WorldGen/WorldGenerator*.cs`, `GenMath.cs`) and the location placement (`SeedLocations.cs`)
are written for the editor, following the game's formulas and rules so that they give the same
terrain and places (checked against values recorded in the game).

These files hold data read from the game's files, which remains theirs; the MIT License does not
cover them:

- In `editor/tests/fixtures/`: `locations.json`, `terrain-modifiers.json`, `prefabs.json`, `pieces.json`,
  `piece-support.json`, `piece-place.json`, `zdo-keys.json`, `cave-rocks.json`, `piece-cost.json`,
  `vegetation.json` and `dungeon-rooms.json`, some of the game's location rules, terrain modifiers,
  prefabs, build pieces, pieces' support and placement, object data names, a cave roof rock's mesh,
  pieces' costs, vegetation rules and dungeons' rooms, for the tests (the editor reads them from the
  player's game; `editor/Core/WorldGen/` and the app hold none of the game's data).

The game's textures and models are not in this repository or in the releases: the editor copies
them from the player's own Valheim install, on their computer.

## Third-party code

- The editor reads Expand World Data's YAML files through [YamlDotNet](https://github.com/aaubry/YamlDotNet)
  (MIT), the library that mod depends on: the copy found under the player's BepInEx plugins folder is loaded at run
  time (`editor/Core/App/YamlReader.cs`); the editor ships none and its tests use the NuGet package.

- `editor/Core/WorldGen/FastNoise.cs`: FastNoise, trimmed to the 2D cellular and simplex noise the world
  generator uses, MIT License, Copyright (c) 2017 Jordan Peck (its notice is kept in the file).
- `editor/Core/App/Smolv.cs`: a C# port of the decoder in [SMOL-V](https://github.com/aras-p/smol-v),
  MIT License, Copyright (c) 2016-2024 Aras Pranckevicius.
- `editor/Core/App/Crunch.cs`: a C# port of the DXT1 and DXT5 decoding in crunch's `crn_decomp.h`
  ([DaemonEngine/crunch](https://github.com/DaemonEngine/crunch), the Unity format of
  [Unity-Technologies/crunch](https://github.com/Unity-Technologies/crunch/tree/unity)), zlib
  License, Copyright (c) 2010-2016 Richard Geldreich, Jr. and Binomial LLC (its notice is kept in
  the file).
- [SPIRV-Cross](https://github.com/KhronosGroup/SPIRV-Cross) (Apache License 2.0), through Silk.NET's
  native build of it, turns the terrain shader from Valheim for Windows into GLSL.
- Libraries the editor is built with, each under its own license: [Avalonia](https://github.com/AvaloniaUI/Avalonia)
  (MIT), [Silk.NET](https://github.com/dotnet/Silk.NET) (MIT), [SSH.NET](https://github.com/sshnet/SSH.NET)
  (MIT), [Roslyn](https://github.com/dotnet/roslyn) (MIT), for scripts, and, to read the game's models
  and textures from its own files, [AssetsTools.NET](https://github.com/nesrak1/AssetsTools.NET) (MIT)
  and [BCnEncoder.NET](https://github.com/Nominom/BCnEncoder.NET) (MIT or Unlicense); for Claude's
  connection, the [MCP C# SDK](https://github.com/modelcontextprotocol/csharp-sdk) (Apache License 2.0)
  with ASP.NET Core (MIT).
