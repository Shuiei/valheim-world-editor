# Dungeons

Frost Caves, burial chambers, sunken crypts, infested mines and Valheim's other dungeons are not dug
into the ground. The game builds them from ready-made **rooms**, 5000 m above their entrance on the
surface, and the entrance's door teleports you up there. Each dungeon is one object in the save that
holds its **list of rooms**: which room, where, and turned which way. When a player comes near, the
game builds the rooms from that list, whatever it holds. The **Dungeon** tool edits that list.

## Opening a dungeon

1. Open the area over a dungeon's entrance (the map's **Location markers** show the entrances).
2. Pick **Dungeon** on the tool rail. The panel lists the area's dungeons, with their kind and number
   of rooms.
3. **Go inside** takes the view up to the dungeon and cuts its roofs away, so you look down into its
   rooms. The **Cut** slider sets how high above the floor the cut is; 0 shows the roofs again.

Up there the camera no longer follows the ground: orbit, slide and fly around the rooms as usual.
**Dungeon rooms** in the View panel shows or hides them.

## Adding rooms

Every room has **openings**, the doorways where it meets the next room. The game only joins openings
of the same kind: a Frost Cave has plain cave openings, ice openings, shrine openings and others. Rooms
such as an ice corridor join one kind to another.

- A green frame marks each **open end**: an opening no room meets yet.
- Pick a room in the list (the finder above it narrows the list), then point at an open end: the room
  shows there, see-through, joined the way the game joins rooms.
- **Turn** (R) joins it by another of its openings, when it has several of that kind.
- Click to add it.

Its box turns red, and the panel says why, when it overlaps another room or goes past the dungeon's
space (64 × 64 m around its zone's middle for most kinds). The game keeps its own rooms clear of both;
a list made here is built anyway, so it is only a warning.

With **With what the game puts in them** on, a room comes with what the game would have made in it at
that place: chests, creatures' spawners, torches, braziers, ice, decorations. The editor rolls them
the way the game does when it first generates a dungeon, so they are the same the game would have
chosen.

**Close open ends** puts the game's end cap on every open end, so nothing leads out of the dungeon.

## Selecting and deleting

Click a room to select it (its box shows), then **Delete room** (Del). What stands in the room
(chests, spawners, ice…) goes with it. The rooms next to it have open ends again: **Close open ends**
caps them.

Each change is one step of the history: Ctrl+Z puts things back. **Save to world** writes it, or
**Apply live** in live mode.

## Building in a dungeon

**Build here** gives the Workshop's **Build** over the dungeon: pick a piece, then click on a room's
floor or wall, or on a piece already there. Pieces snap as with the game's hammer.

In game every building piece needs support from the ground. A room's floors and walls count as
ground, so pieces resting on them hold. Pieces hanging in the open, away from the rooms, break as soon
as a player comes near.

## Good to know

- **Doors** between rooms (the cloth curtains of Frost Caves, crypt gates) are objects the game adds
  where two rooms meet. Rooms added here have none; place one with the Place tool where you want it.
- Some of what the game puts in rooms (icicles, crystals, fish) settles on the nearest surface in game,
  so it may sit a little differently from where the editor shows it.
- The game draws dungeons dark, lit by their torches. The editor lights them like the outside, to see
  what you edit.
- Rooms come with the game's look (copied from your Valheim). When the editor is updated, the copy
  runs once more to add them.
