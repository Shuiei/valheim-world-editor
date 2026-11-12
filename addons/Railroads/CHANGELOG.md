# Changelog

Changes to the Railroads add-on for the Valheim World Editor. Newest first. It goes out with every editor release,
as its own zip and under its own version.

## v0.7.0 — 2026-11-12

### Added
- **Buffer stop and train workshop.** Follows Railroads 0.5.0: the buffer stop (0.4.15) is drawn and offered under
  Track, its 0.6 m of track laid as the mod lays it, and the train workshop (0.5.0) under Other.
- **Torches on vehicles.** The torch at each of a locomotive's or wagon's lamps (0.5.0: a wood torch, none, or a wisp
  torch) shows in the inspector, where it can be changed.

## v0.6.0 — 2026-11-09

### Added
- **Crossing lights and barrier.** Follows Railroads 0.4.9, whose crossings (since 0.4.7) are drawn at rest, the lamps
  unlit and the red-striped arm up, and offered in the Workshop under Other.
- **Turned wagons stay turned in a pasted train.** Since 0.4.4 a wagon keeps the way it faced when coupled; a pasted
  train's copy keeps its wagons turned as the original's, and the inspector lists them.

### Changed
- The feedback post, retired in 0.4.5, is no longer offered in the Workshop; posts in worlds still show. The chains a
  piece was paid with (0.4.6) show in the inspector, read-only.

## v0.5.1 — 2026-11-02

### Fixed
- **A fitted track the game ignores is drawn as the game shows it.** Follows Railroads 0.4.1, which lays no line for a
  fitted track with a shape no game builds (a far end more than 33 m away, or a way not about a metre long): the editor
  draws it as the piece's own model, unbent, and puts no train on it.

### Changed
- Railroads 0.4.1's train load and a sent train's coal rate show in the inspector, read-only; a pasted or moved train
  leaves the coal rate behind with its trip. The 2 new gauges on the boiler are not drawn, like the levers and rods.

## v0.5.0 — 2026-10-31

### Added
- **Fitted track.** Follows Railroads 0.4.0, whose new fitted track joins 2 track ends that do not line up, up to
  32 m: it is drawn bent between its ends as the game builds it, trains placed on it stand on its line, and it is listed
  in the Workshop under Track. Pasted or moved turned, it keeps its shape, turned with it (needs the editor 1.119.0).
  Its far end and the ways it leaves and reaches its ends show in the inspector.

### Changed
- Railroads 0.3.7 and 0.3.8 change nothing the add-on reads: a station sign's turn now picks the side of the ghost
  train in the game, and tame boars and carts ride on a flatbed with the same keys as the pieces built on it, which the
  editor already moves, pastes and keeps with their flatbed.

## v0.4.0 — 2026-10-31

### Added
- **Railroads' pieces in the Workshop without a live game.** With Railroads installed, the Workshop's **Mod pieces**
  tab lists its track, stations and trains by their names in the game, even before a live game told the editor of
  them. Their cost is not known offline: the Materials panel lists them under "No known recipe".

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
