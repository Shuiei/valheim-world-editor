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
- **Size** and **Levels**: about how many rooms, on how many levels (up to 16) joined by stairs.
  The boss waits on the last level.
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
  axes, with corridors between them; side rooms often come in pairs on either side of a hall. Some
  corridors bend on the way (often in catacombs, warrens and holds), and some rooms are entered on
  their long side.
- **Rooms take shapes.** Besides plain boxes there are crossings and shrines with their corners
  notched, chapels, halls and throne rooms that narrow into an apse, store rooms and quarters that
  lose a corner or neck in at the door, and arenas notched into an octagon. The walls, the beams
  and the furniture follow the shape.
- **Every room is furnished for its purpose, and not always the same way.** Each kind of room has
  two to four furnishings, picked by the seed:
  - Halls: colonnades with an altar at the far end; a mead hall round a fire with trestle tables;
    a hall of the dead with a sarcophagus on a dais between statues; or a bare, echoing hall of
    fallen pillars.
  - Chapels: pews facing an altar between two statues; a raised altar in a ring of braziers; or a
    nave of rugs and candles under long hangings.
  - Burial chambers: rows of sarcophagi; one tomb on a dais among skulls and candles; or sarcophagi
    set at odd angles among the bones. Ossuaries: bones along the walls, a heap in the middle, or
    stacks in rows. Galleries: niches, standing sarcophagi, statues, or bare and cobwebbed.
  - Barracks: beds along the walls with a table; bedrolls round a fire with a weapon stand; or
    bunks with chests at their feet. Store rooms: stock along the walls, stacked in a corner with a
    cart, or shelves in rows. Studies: shelves round a table, in aisles, or a scriptorium of desks.
  - Feast halls: long tables down the middle, or the high table on a dais before a fire. Throne
    rooms: colonnades, a war room with a long table, or fallen columns round the dais.
  - Arenas: a ring of columns round the boss; the boss enthroned on a dais over heaps of skulls; a
    crypt of columns and sarcophagi under chains; or a pit, the floor sunk a metre below ledges
    round the walls. Shrines can be sunk the same way, the idol down in a ring of candles.
  - Cell blocks have barred cells on each side of an aisle.
- **Rooms are built up inside, not just furnished.** In a stone or grausten dungeon a great hall,
  throne room or chapel may carry a loft over the way in: a deck 4 m up on posts, with a rail and
  stairs up one side, braziers and a sentinel on it. A burial chamber may hold a vault, an inner
  chamber of walls against the far wall with the sarcophagus inside and candles at its opening.
  Alcoves off a room hold a sarcophagus across, a statue between candles, a bed in its bay or a
  shelf and crates; niches off a corridor a single statue, a stand or a barrel.
- **Things come in groups that belong together.** Beside its main feature, a room has one or two
  clusters in its corners that suit it: a camp (bedroll, fire, firewood), a meal (table, seats,
  food), a scribe's desk (table, chair, candles, chest), stores (crates stacked, barrels), an
  armoury (weapon and armour stands), an altar (statue, candles, offerings), bones, a workbench.
- **One piece a room is remembered by.** Big rooms may get a hero piece that stands once in the
  whole dungeon: a dolmen, a fallen giant's statue in parts, a grave slab, a giant's skull or ribs
  in a warren, an altar of crystal in a hold, a cart in a keep.
- **Corridors are not all the same corridor.** A long one may be closed midway by a gate under a
  band of wall (an iron gate in a crypt, a door in a keep, a sliding door in a hold), half
  collapsed (rubble, broken wall, a fallen pillar, the way open along one side), lined with bones
  along both walls (catacombs), carpeted with a runner (keeps, temples, holds), hung with vines
  (crypts, ruins) or edged with sharpened stakes (warrens).
- **Rooms are lit differently**: standing torches along the walls, sconces on them, or a brazier
  or two in the corners; the dark leaves most rooms unlit.
- **Floors and ceilings vary.** In a stone or grausten dungeon the grand rooms get black marble tiles and the
  lived-in ones wooden boards, half the time. Raised stone daises with steps, pilasters along the
  walls, chains and hooks hanging from the roofs, cobwebs in the corners.
- **Doors close every room.** Monsters do not open doors, so each room's monsters wait behind its
  door, and the stairs between levels have doors too.
- **Some rooms rise through two levels**: great halls, chapels and the arena.

**Place in the world** puts its **entrance** on the ground at the middle of the view, facing you,
and the dungeon 5000 m above it, where the game keeps its own dungeons. The entrance is the game's
own for the biome: a burial chamber's mound in the Meadows and the Black Forest, a sunken crypt's
door in the Swamp, a Frost Cave's mouth in the Mountains and the Deep North, an infested mine's in
the Mistlands, a Morgen hole in the Ashlands. Its door leads up into the dungeon and the door up
there leads back out, as in the game's own dungeons; the dungeon is laid out where the game lays
that entrance's own (a Frost Cave's goes to the middle of the zone). The Plains have no dungeon of
their own: there a portal on the ground leads in and its twin in the entrance hall leads back, both
written linked under a tag such as *dg123*. One dungeon fits over a zone (64 × 64 m): with one there
already, the editor says so and places nothing.

The view stays at the entrance. **Go to the dungeon** takes it up, looking down into the first level
with the roofs cut away; **Go to the entrance** brings it back down. Like everything else in the
editor, it is one step of the history (Ctrl+Z takes it all back) until you **Save to world** or
**Apply live**. The dungeon is written as a ruin, with no builder, like the game's own: monsters leave
its walls alone, and taking it apart gives back a third of the materials.

**Open in the Workshop** opens it as a blueprint (in the game's Homestead folder), to change it there:
open a chest to change what it holds, move rooms' furniture, add your own touches. The blueprint
keeps what its objects hold (the key in its chest, the signs, the boss and its stars) and that it is
a ruin. It is tagged *dungeon*, and a **portal** stands in its entrance hall where players arrive:
pasted into a world from the Blueprints tab, it goes up behind an entrance as a generated dungeon
does (see [Dungeons from blueprints](#dungeons-from-blueprints)). Built in game with Homestead, it is
only the objects: no key, no boss.

From the Workshop itself, the Build panel's **Library** has **Generate dungeon…**: the same choices
(building pieces only), then **Open on the plot** or **Add to the plot** (with what is already there).

All of a dungeon of building pieces holds in the game, high in the sky. Its floors and roofs are
black marble slabs, the one piece the game never asks for support, and everything else stands on
them.

## Dungeons from blueprints

Any blueprint can be a dungeon. In the Workshop, switch **A dungeon** on when you save it (the
Blueprints tab's **Details** has the same switch; it is the tag *dungeon*). Pasted into a world from
the Blueprints tab, such a blueprint is not put on the ground: the entrance of the biome's dungeon
goes where you click, facing you, and the building 5000 m above it, as **Place in the world** does
for a generated dungeon.

Put a **portal** in the building where players should arrive, facing the way they should look: the
entrance's door out opens there, and the portal itself is left out. Without a portal, players arrive
at the building's middle, facing north. Where the biome has no dungeon of its own (the Plains), the
portal stays, linked to one on the ground.

Pieces hold in the sky only on what the game never asks support for: black marble floors
(`blackmarble_floor_large`) make the ground of a dungeon of pieces, and everything else stands on them.

## Opening a dungeon

1. Open the area over a dungeon's entrance (the map's **Location markers** show the entrances).
2. Pick **Dungeon** on the tool rail. The panel lists the area's dungeons, with their kind and number
   of rooms.
3. **Go inside** takes the view up to the dungeon and cuts its roofs away, so you look down into its
   rooms. The **Cut** slider sets how high above the floor the cut is; its right end shows the roofs again,
   its left end cuts at the floor. **Go to entrance** takes the view back down to the dungeon's entrance
   on the ground, with nothing cut.

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
