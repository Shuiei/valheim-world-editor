# Changelog

Changes to the Expand World add-on for the Valheim World Editor. Newest first. It goes out with every editor release,
as its own zip and under its own version.

## v0.3.0 — 2026-11-17

### Added
- **Custom objects placed and edited.** The new **Custom object** tool (under **Mods** on the 3D editor's rail) lists
  the data entries of `BepInEx/config/data/*.yaml`, `expand_world/expand_data*.yaml` and those written among Expand
  World Prefabs' rules (`expand_world/expand_prefabs*.yaml`), and gives the one chosen to an object: a new one placed
  where you click, among the objects the rules use an entry on, or any object you click. Its values are written as the
  game reads them, a component's fields ("Piece.m_name") and a size by axis ("scale") included. Values the mod works
  out as it makes an object (a parameter, a list, a range, items) are left out, and the card names them. Needs the
  editor 1.126.0.
## v0.2.0 — 2026-11-17

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
