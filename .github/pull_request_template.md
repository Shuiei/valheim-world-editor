## What and why

<!-- What this changes and the reason. Link the issue: "Fixes #123". -->

## Where it shows

- [ ] Editor (Core / Desktop)
- [ ] WorldEditorBridge plugin
- [ ] Docs only

Modes affected: <!-- My game, dedicated server, saved world, Workshop -->

## How it was tested

<!-- Tests added or run, and what was checked by hand (on a local or scratch world, never a live server). -->

- [ ] `dotnet test tests/Desktop.Tests` passes
- [ ] Tried in the app

## Checklist

- [ ] `CHANGELOG.md` updated (and `VERSION` if this is a release)
- [ ] Docs in `README.md` / `docs/` updated if behaviour changed
- [ ] Editor and plugin still agree on the protocol (if the plugin changed)
- [ ] Saved worlds stay readable by the game; backups still made before writing
