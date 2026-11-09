# Changelog

Changes to the Riverheim add-on for the Valheim World Editor. Newest first. It goes out with every editor release,
as its own zip and under its own version.

## v0.2.1 — 2026-11-09

### Fixed
- **Riverheim 1.3 lays worlds out again.** Follows Riverheim 1.3.1, now a plugin of Worldhalla 1.0.0: the add-on loads
  it with the Worldhalla in its BepInEx folder (and says so on the world's card when there is none, as the game needs
  it too) and calls its new generator. The forests are placed as the game places them: Worldhalla works the forest
  factor out from float values, a bit or two apart from Riverheim 1.2's at a third of the points, and the add-on does
  the same for Riverheim 1.3 and later. Worlds saved by Riverheim 1.3, which keeps their settings in Worldhalla's
  header, open with the editor 1.119.3 or later.

### Changed
- Live, from Riverheim 1.3 your Riverheim must be the game's very version, as Worldhalla asks of players who join
  (the same release before 1.3).

## v0.2.0 — 2026-10-21

### Added
- **Clearer cards for Riverheim worlds that cannot open.** A world your Riverheim is too old for shows so in the world
  list, greyed out, before you try to open it; and the editor names this add-on when it is turned off or waiting for
  Riverheim, instead of a world generation mod it does not know.

## v0.1.0 — 2026-10-21

### Added
- **Riverheim worlds in the editor, as an add-on.** Worlds made with Riverheim open, live too, drawn by your own
  Riverheim, and new worlds can be made with it, as the editor did by itself before: it now comes as this add-on,
  turned off or on in Settings like any other.
