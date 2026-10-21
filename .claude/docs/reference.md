# Reference

## Editor (`_src/valheim-world-editor/`)
- `docs/architecture.md`: projects, save-format pipeline, live mode, Claude/MCP, cross-file rules. Read first for any editor change.
- `docs/development.md`: layout, CLI/driver, build, versions, release, tests, fuzzing.
- `docs/modding.md` + `docs/worldeditor.schema.json`: SDK and mod integration contract.
- `docs/claude.md`, `docs/scripting.md`: MCP tools and script API.
- `.claude/skills/valheim-build`: building style and workflow for in-game builds.
- `VERSION` and `CHANGELOG.md` at repo root; one bump per PR.
- CI: CircleCI (tests, releases via approval). Required checks: CodeRabbit approval, CodeQL.

## Game and modding
- BepInEx 5 + HarmonyX. Plugin entry: `[BepInPlugin]` class deriving `BaseUnityPlugin`.
- Game API: `valheim_server_Data/Managed/assembly_valheim.dll` (ZDO, ZNet, ZoneSystem, Player...).
- World format facts and save-format gotchas: editor `docs/architecture.md` and memory notes.
- ServersideQoL upstream: github.com/ArgusMagnus/ValheimServersideQoL.

## Mod sites (MCP servers available in this workspace)
- Thunderstore: `Tie-*` packages, community `valheim`. Also r2modman/Gale source.
- Hexium: valheim.hexium.gg, Thunderstore-compatible API.
- Nexus Mods: editor + plugin is mod 4348. Pages are created on the site by hand.
- Valheimians: read-only build inspiration and PlanBuild blueprints.

## Live server
- `ssh valheim` (OVH). Paths, logs and restart recipe are in memory `ovh-server-access`.
- Bridge for live editing: WorldEditorBridge plugin, HTTP over ssh tunnel port 15182.
