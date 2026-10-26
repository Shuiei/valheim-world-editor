# Changelog

Changes to the ValheimRAFT add-on for the Valheim World Editor. Newest first. It goes out with every editor release,
as its own zip and under its own version.

## v0.2.0 — 2026-10-26

### Added
- **ValheimRAFT 5.1.1.** Checked on a server running it: vehicles are drawn where 5.1.1 builds them, as with 5.0.9.
  Its new iron centre bows are listed under Hull in the Build panel; its build mode toggle, which deletes itself once
  placed, is not offered. The inspector names a vehicle's build mode, its hull shape and the dock clamp it is docked
  at.

### Fixed
- **A pasted vehicle is not docked.** With ValheimRAFT 5.1, a copy of a docked vehicle named the original's dock
  clamp, where the mod would hold it while the original stays. A copy also leaves out the hull shape the mod saved,
  which the mod works out again: a mirrored copy kept the original's.

## v0.1.0 — 2026-10-23

### Added
- **Vehicles are drawn whole.** Each piece of a ValheimRAFT ship or land vehicle is drawn where the mod builds it on
  the vehicle, turned and tilted with it: the pieces on a swivel on its platform as the mod turned or moved it, a sail
  on its mast's yard, a land vehicle parked on a ship where the mod builds it, and what a vehicle carries where it was
  saved; checked against the mod's own building on a server.
- **Vehicles move, copy and delete as one**, live too. Any piece selected takes the whole vehicle, with what it
  carries; a pasted vehicle gets ids of its own, and its ropes, sails, switches and plants follow the copy.
- **Sails, ropes and rope ladders.** Custom sails are drawn between their corners in their colour, ropes from each
  rope anchor to the tie point the mod picks, rope ladders with their ropes and rungs down to the ground or the water.
- **The mod's values in words** in the inspector, and its pieces under the vehicle hammer's headings in the Build
  panel.
