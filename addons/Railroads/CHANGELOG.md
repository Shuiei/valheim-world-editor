# Changelog

Changes to the Railroads add-on for the Valheim World Editor. Newest first. It goes out with every editor release,
as its own zip and under its own version.

## v0.3.0 — 2026-10-29

### Added
- **Drawbridges.** Follows Railroads 0.3.6, which has a drawbridge (since 0.3.1): it is drawn with its leaves, chains
  and counterweights, lowered or raised as the mod keeps it, and **Raised** in the inspector raises or lowers it.
  Trains placed on it stand on its track; a station sign beside it stops trains on the straight track only, as in the
  game.
- **The feedback post** (0.3.0) is drawn and offered in the Build panel, under Other.

### Changed
- **Couplers.** Since 0.3.0 the mod builds each vehicle's couplers apart from its body; they are drawn again, at each
  end of the wagons and at the back of the locomotive. An older Railroads still shows them on the body.
- **The old station is offered again** in the Build panel, as the mod put it back on the hammer in 0.3.5.
- **A pasted or moved train forgets who sent it**, with the rest of its trip.
- The inspector names what a piece was paid with (0.3.2) and, for coal, the fuel the server may use instead (0.3.6).

## v0.2.0 — 2026-10-26

### Added
- **Train names.** Follows Railroads 0.2.9, whose trains have names (since 0.2.7): the inspector names a train's
  name, which you can change, and the number the server gave it. A pasted train keeps its name and gets a number of
  its own from the server, as a train just built. The locomotive is drawn with the name boards on its boiler.

### Changed
- **Railroads 0.2.9.** The portal wagon is drawn as the mod now builds it, without posts on its open side. Both
  models come from your copy of the mod, so an older Railroads still shows its own.

## v0.1.0 — 2026-10-23

### Added
- **Track, stations and trains are drawn.** Every piece of Railroads shows in the editor's 3D view as the game builds
  it, from the mod's own models: track, switches, crossings, loops and junctions, station signs, consoles and old
  stations, and the locomotive, wagons, flatbed and hand cart on their wheels.
- **Pieces stay right wherever you put them.** Track, trains and station signs you place, paste, move or turn work in
  the game as if built there, saved or live: track keeps its turn, vehicles stand on the track under them, signs stop
  trains at the track beside them.
- **Trains and flatbeds stay whole.** Moving, turning, copying or deleting a locomotive takes its wagons, a flatbed
  what is built on it (also when the flatbed is in a train); a pasted train pulls its own copied wagons, a pasted flatbed carries its own deck.
- **Pasted copies are pieces of their own.** Pasted trains leave the originals' driver and trip behind.
- **The mod's data in words.** The inspector names what the mod keeps on its pieces and shows a train's track lines,
  wagons and trip in words.
- **Build panel groups.** The mod's pieces are listed as Track, Stations and Trains; retired pieces are not offered.
