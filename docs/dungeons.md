# Dungeons

Frost Caves, burial chambers, sunken crypts, infested mines and Valheim's other dungeons are not dug
into the ground. The game builds them from ready-made **rooms**, 5000 m above their entrance on the
surface, and the entrance's door teleports you up there. Each dungeon is one object in the save that
holds its **list of rooms**: which room, where, and turned which way. When a player comes near, the
game builds the rooms from that list, whatever it holds. The **Dungeon** tool edits that list.

## Generating a dungeon

**Generate a dungeon**, at the top of the Dungeon panel, makes a whole dungeon from a few choices.
The same choices and seed always make the same dungeon; **Randomize** draws another seed. A plan of
each level shows what they make as you change them: the way through in light brown, the boss's
arena in red, treasure in gold, the room with the key in green, doorways as dots.

- **Made of**: *Building pieces*, rooms built as a player would build them; or *The game's own
  rooms*, the biome's dungeon rooms (a Frost Cave, a burial chamber...) joined the way the game
  joins them.
- **Biome**: its monsters, chests, loot, lights and what lies about; its style and walls too,
  unless picked.
- **Style**: which rooms it has, and how they are laid out.
  - *Crypt*: burial chambers, ossuaries, tombs and chapels.
  - *Catacombs*: long galleries lined with the dead.
  - *Temple*: chapels, shrines and a study.
  - *Fortress*: barracks, armory, feast hall, kitchen, forge and throne room.
  - *Prison*: cell blocks and torture chambers.
  - *Dvergr hold*: mine galleries, workshops and quarters.
  - *Goblin warren*: dens and totem halls.
  - *Ruins*: a mix, crumbling.
- **Walls**: stone, black marble, wood, stave, grausten, Dvergr metal or goblin palisade.
- **Size** and **Levels**: about how many rooms, on how many levels joined by stairs. The boss
  waits on the last level.
- **Monsters**: how many. **Monsters come back** puts the game's spawners, whose monsters return a
  while after dying. Without it, the monsters themselves are placed, once. The boss is always placed
  once, with a star more for more monsters.
- **Loot**, **Light**, **Furniture**: how many chests and treasures, torches and braziers,
  furniture and things lying about.
- **Ruin**: rubble, fallen pillars, broken pots, torches gone out.
- **Ways round**: how often two rooms far apart on the way, but close together, are joined, so
  there is more than one way round.
- **Boss behind a gate, its key in a chest**: the arena's gate opens with the crypt key, which lies
  in a chest in a room far from the gate. Players who already hold the key (from the Elder) open it
  too.
- **Hidden caches**: small rooms of treasure behind a cracked wall to break (stone walls) or behind
  a tapestry.
- **Name**: written on a sign in the entrance hall.

What it is like:

- **The layout reads like a building.** Rooms open in the middle of their walls and line up along
  axes, with corridors running straight between them. Side rooms often come in pairs on either side
  of a hall.
- **Every room is furnished for its purpose.**
  - Halls have columns, a runner rug and a throne or an altar at the far end.
  - Chapels have pews facing an altar between two statues.
  - Burial chambers have rows of sarcophagi; ossuaries have bones along every wall.
  - Feast halls have long tables with food, benches and a hearth.
  - Cell blocks have barred cells on each side of an aisle.
  - The arena has a ring of columns and a great rug, with the boss in the middle.
- **Doors close every room.** Monsters do not open doors, so each room's monsters wait behind its
  door, and the stairs between levels have doors too.
- **Some rooms rise through two levels**: great halls, chapels and the arena.

**Place in the world** puts it 5000 m above the middle of the view, where the game keeps its own
dungeons. It goes higher if something is already up there. A portal on the ground there leads in,
and its twin in the entrance hall leads back. Both are written linked, under a tag such as *dg123*.
Like everything else in the editor, it is one step of the history (Ctrl+Z takes it all back) until
you **Save to world** or **Apply live**. The dungeon is written as a ruin, with no builder, like the
game's own: monsters leave its walls alone, and taking it apart gives back a third of the materials.

**Open in the Workshop** opens it as a blueprint (in the game's Homestead folder), to change it there:
open a chest to change what it holds, move rooms' furniture, add your own touches. The blueprint
keeps what its objects hold (the key in its chest, the signs, the boss and its stars) and that it is
a ruin. Placed in a world from the editor, it comes back whole. Built in game with Homestead, it is
only the objects: no key, no boss.

All of a dungeon of building pieces holds in the game, high in the sky. Its floors and roofs are
black marble slabs, the one piece the game never asks for support, and everything else stands on
them.

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

## Doors

Where two rooms meet, the game sometimes puts a door: a Frost Cave's cloth curtains and ice walls, a
crypt's gates, the mines' doors. Each kind of dungeon has its own, for each kind of opening.

- With **Doors where the game might put them** on, an added room gets one where it joins, by the
  game's own chance.
- A square marks every place two rooms meet and a door can stand: **blue** with a door, **orange**
  without. Click it to put the dungeon's door there, or to take the door away.

## Selecting and deleting

Click a room to select it (its box shows), then **Delete room** (Del). What stands in the room
(chests, spawners, ice…) and the doors where it met other rooms go with it. The rooms next to it have
open ends again: **Close open ends** caps them.

Each change is one step of the history: Ctrl+Z puts things back. **Save to world** writes it, or
**Apply live** in live mode.

## Building in a dungeon

**Build here** gives the Workshop's **Build** over the dungeon: pick a piece, then click on a room's
floor or wall, or on a piece already there. Pieces snap as with the game's hammer.

In game every building piece needs support from the ground. A room's floors and walls count as
ground, so pieces resting on them hold. Pieces hanging in the open, away from the rooms, break as soon
as a player comes near.

## Good to know

- Some of what the game puts in rooms (icicles, crystals, fish) settles on the nearest surface in game,
  so it may sit a little differently from where the editor shows it.
- The game draws dungeons dark, lit by their torches. The editor lights them like the outside, to see
  what you edit.
- Rooms come with the game's look (copied from your Valheim). When the editor is updated, the copy
  runs once more to add them.
