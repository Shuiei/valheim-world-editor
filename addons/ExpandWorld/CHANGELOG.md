# Changelog

Changes to the Expand World add-on for the Valheim World Editor. Newest first. It goes out with every editor release,
as its own zip and under its own version.

## v0.2.0 — unreleased

### Added
- **Persistent events placed where you want them.** The new **Persistent event** tool (under **Mods** on the 3D
  editor's rail) lists the events of Expand World Data's `expand_persistent_events.yaml` and starts the one chosen
  where you click, at a size between its smallest and largest radius: the world's save then holds it as active, as
  if the game had started it there. The objects the event makes when it starts are placed round it as the file
  says (their counts, distances, spacing, slope and water), inside the area open. A click inside an active event
  can stop it instead. Needs the editor 1.125.0, a saved world (not live mode) and the mod's "Persistent event
  data" switch on. The event's location, its start messages and commands, object data and blueprints are left
  out.

## v0.1.0 — 2026-10-21

### Added
- **Expand World worlds in the editor, as an add-on.** Worlds made with Expand World Size, Rivers and Data are
  generated, drawn and predicted as the mods make them, live too, from the mods' settings and data files, as the
  editor did by itself before: it now comes as this add-on, turned off or on in Settings like any other.
