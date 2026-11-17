# Changelog

Changes to the Expand World add-on for the Valheim World Editor. Newest first. It goes out with every editor release,
as its own zip and under its own version.

## v0.3.0 — unreleased

### Added
- **Custom objects placed and edited.** The new **Custom object** tool (under **Mods** on the 3D editor's rail) lists
  the data entries of `BepInEx/config/data/*.yaml`, `expand_world/expand_data*.yaml` and those written among Expand
  World Prefabs' rules (`expand_world/expand_prefabs*.yaml`), and gives the one chosen to an object: a new one placed
  where you click, among the objects the rules use an entry on, or any object you click. Its values are written as the
  game reads them, a component's fields ("Piece.m_name") and a size by axis ("scale") included. Values the mod works
  out as it makes an object (a parameter, a list, a range, items) are left out, and the card names them. Needs the
  editor 1.126.0.

## v0.1.0 — 2026-10-21

### Added
- **Expand World worlds in the editor, as an add-on.** Worlds made with Expand World Size, Rivers and Data are
  generated, drawn and predicted as the mods make them, live too, from the mods' settings and data files, as the
  editor did by itself before: it now comes as this add-on, turned off or on in Settings like any other.
