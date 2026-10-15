# Dungeons

![A Burial Chambers dungeon of the game's own rooms, cut open, with the Dungeon panel's room editing](images/dungeon-rooms.jpg)
*A dungeon of the game's own rooms (Burial Chambers, 18 rooms), 5000 m up and cut 2 m above the floor. Orange and
blue squares mark where rooms meet (a door, or a doorway the game may fill); the panel lists the area's dungeon, the
cut, the rooms to add, and **Turn (R)**, **Delete room (Del)** and **Close open ends**.*

The **Dungeon** tool (on the tool rail, under CREATE) makes whole dungeons from a few choices and puts them in your
world behind an entrance, as the game does with its own. It also edits the game's own dungeons (Frost Caves,
burial chambers, sunken crypts, infested mines...) room by room, and lets you build inside them.

## How the game keeps dungeons

Valheim's dungeons are not dug into the ground. Each one is a **location** on the surface (a burial chamber's
mound, a Frost Cave's mouth) whose door teleports you to the dungeon itself, **5000 m above** it, where the game
treats everything as indoors.

The game builds its own dungeons from ready-made **rooms**. A dungeon is one object in the save that holds its
**list of rooms**: which room, where, and turned which way. When a player comes near, the game builds the rooms
from that list, whatever it holds, without checking it against its own rules. Until a player has been near, the
list is empty: the game lays a dungeon out the first time someone comes.

The editor uses both sides of this:

- A **generated dungeon** of building pieces is ordinary pieces placed 5000 m up, with the game's own entrance on
  the ground leading to it.
- A generated dungeon of **the game's own rooms**, and every edit of an existing dungeon, writes that list of
  rooms. The game then builds what the list says.

## Generating a dungeon

![The generator's plan, scrolled down in the Dungeon panel](images/dungeon-generate.jpg)
*The Dungeon panel's **Generate a dungeon**, scrolled to its plan: the **Level 1** and **Level 2** buttons, the
summary of what was made, and **Place in the world** and **Open in the Workshop**.*

**Generate a dungeon** is at the top of the Dungeon panel. It is open when the area has no dungeon, folded when it
has one.

### How to use it

1. Open an area of a world and pick **Dungeon** on the tool rail.
2. Choose the **Biome**, **Size**, **Levels** and the rest (below). A moment after each change the dungeon is made
   again and its plan drawn. The same choices and seed always make the same dungeon.
3. **Randomize** draws another seed; any whole number in **Seed** makes another dungeon with the same choices.
4. Look at each level with the **Level 1**, **Level 2**... buttons under the plan.
5. Turn the view so that you face the place where the entrance should go, centred on it, then click **Place in
   the world**. Or click **Open in the Workshop** to change it there first.
6. **Go to the dungeon** takes the view up into it; **Go to the entrance** brings it back down.

### The choices

| Control | What it does |
|---|---|
| **Made of** | **Building pieces**: rooms built as a player would build them, furnished for what they are. **The game's own rooms**: the biome's dungeon rooms (a burial chamber, a Frost Cave...) joined the way the game joins them (see [Made of the game's own rooms](#made-of-the-games-own-rooms)). Not shown in the Workshop, which only takes building pieces. |
| **Biome** | **Meadows**, **Black Forest** (the default), **Swamp**, **Mountain**, **Plains**, **Mistlands**, **Ashlands**, **Deep North**. It gives the monsters, chests, loot, lights and what lies about, the entrance on the ground, and the style and walls unless you pick them. |
| **Size** | **Small**, **Medium** (the default), **Large**, **Huge**: about 5, 8, 12 or 17 rooms on each level, and how far a level may spread. |
| **Monsters: × 1** | How many monsters: **none** to 3 times the usual, in steps of 0.25. |
| **Monsters come back (spawners)** | On: the game's monster spawners, whose monsters come back a while after being killed. Off: the monsters themselves, placed once. The boss is always placed once. |
| **Style** | **The biome's**, or **Crypt**, **Catacombs**, **Temple**, **Fortress**, **Prison**, **Dvergr hold**, **Goblin warren**, **Ruins**: which rooms it has and how they are laid out. |
| **Walls** | **The biome's**, or **Stone**, **Marble**, **Wood**, **Stave**, **Grausten**, **Dvergr**, **Goblin**. |
| **Levels** | **1 level** to **16 levels** (2 by default), one under another, joined by stairs. The boss waits on the last. |
| **Loot: × 1** | How many chests and treasures: **none** to twice the usual. |
| **Light** | **Dark**, **Dim** (the default) or **Bright**: how many torches and braziers. |
| **Furniture** | **Bare**, **Furnished** (the default) or **Rich**: how much furniture, and how much lies about. |
| **Ruin: 15 %** | How ruined it is (0 to 100 %): rubble, fallen pillars, broken walls and pots, torches gone out. Picking a style sets it to that style's own (Crypt 15 %, Catacombs 30 %, Ruins 70 %...). |
| **Ways round: × 1** | How often rooms far apart along the way, but close in space, are joined, so there is more than one way round. **none (a tree)** to twice the usual. |
| **Boss** | **The biome's boss**, **No boss**, or any biome's boss or elite creature by name (`Troll`, `Draugr_Elite`, `StoneGolem`, `GoblinBrute`, `SeekerBrute`...). |
| **Boss behind a gate, its key in a chest** | The arena's gate opens with the crypt key, which lies in a chest far from it. Players who already hold the key (from the Elder) open it too. Off: grand double gates, open to all. |
| **Hidden caches** | Small rooms of treasure behind a cracked wall to break (stone walls) or behind a tapestry. |
| **Name** | Written on a sign in the entrance hall. Empty: a name made up from the seed, shown greyed in the box (`Barrow of Sigrun`, `Keep of the Grey Jarl`). |
| **Seed** and **Randomize** | The dungeon's own number: everything random about it comes from it. |

With **The game's own rooms**, only **Biome**, **Size**, **Monsters** and the seed are used: the other rows are
hidden, and **Monsters come back** changes nothing (the game's rooms always bring spawners).

What each biome brings when nothing else is picked:

| Biome | Style | Walls | Boss | Entrance on the ground |
|---|---|---|---|---|
| Meadows | Crypt | Stone | `Skeleton_Hildir` | A burial chamber's mound |
| Black Forest | Crypt | Stone | `Troll` | A burial chamber's mound |
| Swamp | Crypt | Stone | `Draugr_Elite` | A sunken crypt's door |
| Mountain | Fortress | Stone | `StoneGolem` | A Frost Cave's mouth |
| Plains | Goblin warren | Goblin | `GoblinBrute` | None of its own: a pair of linked portals |
| Mistlands | Dvergr hold | Dvergr | `SeekerBrute` | An infested mine's entrance |
| Ashlands | Fortress | Grausten | `Charred_Melee_Dyrnwyn` | A Morgen hole |
| Deep North | Temple | Stave | `JotunWarrior` | A Frost Cave's mouth |

### The plan

The plan draws the level shown from above:

- rooms on the way through in light brown, side rooms in darker brown, corridors in dark grey;
- the boss's **arena** in red, the **treasury** and **hidden caches** in gold, the room with the **key** chest in
  green;
- doorways as dots: the boss's locked gate magenta, hidden ways cyan, open arches green, doors and gates orange.

Point at a room for its name. Under the plan, the summary says what was made, for example `Barrow of Sigrun: crypt
of stone, 2 level(s), 15 rooms, 2140 objects; the key is in the ossuary on level 2`, and anything that could not be
made (`no room for stairs down from level 3; the levels below are left out`).

### How the generator lays out a dungeon

A dungeon of building pieces is planned on a grid of 2 m tiles, level by level, then built and furnished:

1. **The entrance hall** opens the first level. Players arrive there, before its back wall, with the dungeon's
   name on a sign beside them.
2. **The way through.** About two rooms in five of each level are the style's main rooms in turn (a crypt's great
   hall, crossing, gallery and chapel; a fortress's guard room, hall, crossing and throne room), each joined to the
   one before, mostly straight ahead.
3. **Down or to the boss.** Each level but the last ends in **stairs down**: an 8 × 24 m stairwell whose stairs
   lead to a foot on the level below, which starts that level. Levels are 6 m apart. On the last level the way ends
   in an **antechamber**, the boss's **arena** (24 × 24 m) and the **treasury** beyond it.
4. **Side rooms** branch off, most often off crossings and long halls. Each kind is picked by the style's weights.
5. **Wings.** A side room often grows off the last side room placed, and what it is leans to what that one is:
   a kitchen by the feast hall, cells and a torture chamber by the guard room, an ossuary and a tomb by the burial
   chamber, a forge by the workshop and the store. Related rooms are five times as likely there.
6. **Twins.** Side rooms often get a twin mirrored on the other side of their hall (a temple nearly always, a
   goblin warren seldom), so the plan reads like a building, along axes.
7. **Ways round.** Rooms three or more rooms apart along the way that happen to lie close are joined, through a
   shared wall or a short corridor: about one join for every five rooms of the level at **Ways round** × 1.
8. **Dead ends.** A corridor or two lead nowhere but a cache at the far end (often in crypts, catacombs, prisons
   and ruins).
9. **The key.** With the boss behind a gate, the key goes into a chest in the side room farthest from the arena, by
   the number of rooms between them.
10. **Hidden caches**: one, or two in a dungeon of more than 20 rooms, off a side room on any level.
11. **Doors.** The boss's gate takes the key (or is a grand double gate); the treasury is behind a gate; cells get
    gates more often; grand rooms (halls, throne rooms, feast halls, chapels) sometimes get grand double gates;
    the rest follow the style (a crypt's wooden doors and iron gates, a warren's open ways and curtains). Where
    stairs go down, an open doorway becomes a gate, so what lives below stays below.

Rooms vary within this:

- **Sizes** follow the room's kind: a storeroom can be 8 × 8 m, a great hall 12–20 m wide and 20–32 m long. A room
  is now and then entered on its long side.
- **Shapes.** Besides plain boxes, crossings, shrines, tombs and arenas often have their corners notched (an
  octagon's feel); chapels, halls and throne rooms may end in an apse; store rooms, quarters, barracks and the like
  may lose a corner or neck in at the door. Walls, beams and furniture follow the shape.
- **Corridors** run straight, or bend on the way and enter the room from the side (often in catacombs, warrens,
  holds and ruins; seldom in a temple). Some rooms meet wall to wall, with no corridor.
- **Two floors high.** Great halls, chapels, throne rooms and the arena may rise 10 m, through the level above
  (rooms are 4 m high otherwise).
- **Alcoves and niches.** Long rooms (galleries, burial chambers, chapels, halls) may get alcoves off their sides.

### How rooms are furnished, lit and guarded

**Every room is furnished for its purpose, and not always the same way.** Each kind of room has two to four
furnishings, picked by the seed:

- Halls: colonnades with an altar at the far end; a mead hall round a fire with trestle tables; a hall of the dead
  with a sarcophagus on a dais between statues; or a bare, echoing hall of fallen pillars.
- Chapels: pews facing an altar between two statues; a raised altar in a ring of braziers; or a nave of rugs and
  candles under long hangings.
- Burial chambers: rows of sarcophagi; one tomb on a dais among skulls and candles; or sarcophagi at odd angles
  among the bones. Ossuaries: bones along the walls, a heap in the middle, or stacks in rows. Galleries: niches,
  standing sarcophagi, statues, or bare and cobwebbed.
- Barracks: beds along the walls with a table; bedrolls round a fire with a weapon stand; or bunks with chests at
  their feet. Store rooms: stock along the walls, stacked in a corner with a cart, or shelves in rows. Studies:
  shelves round a table, in aisles, or a scriptorium of desks.
- Feast halls: long tables down the middle, or the high table on a dais before a fire. Throne rooms: colonnades, a
  war room with a long table, or fallen columns round the dais.
- Arenas: a ring of columns round the boss; the boss enthroned on a dais over heaps of skulls; a crypt of columns
  and sarcophagi under chains; or a pit, the floor sunk a metre below ledges round the walls. Shrines can be sunk
  the same way, the idol down in a ring of candles.
- Cell blocks have barred cells on each side of an aisle.
- Kitchens, forges and workshops have the game's real stations: ovens, cauldrons, fermenters, smelters, the black
  forge, the eitr refinery...

**Built up inside.** In a stone or grausten dungeon a great hall, throne room or chapel may carry a loft over the
way in: a deck 4 m up on posts, with a rail and stairs up one side, braziers and a sentinel on it. A burial chamber
may hold a vault, an inner chamber with the sarcophagus inside. Alcoves hold a sarcophagus, a statue between
candles, a bed or a shelf and crates; niches off a corridor a single statue, a stand or a barrel.

**Things come in groups.** Beside its main feature, a room has one or two clusters in its corners that suit it: a
camp (bedroll, fire, firewood), a meal, a scribe's desk, stores, an armoury, an altar, bones, a workbench. Big
rooms may get a **hero piece** that stands once in the whole dungeon: a dolmen, a fallen giant's statue in parts,
a grave slab, a giant's skull or ribs, an altar of crystal, a cart, a wisp fountain, an eternal pyre, a crane in a
tall hold.

**Each biome's own things.** Vines, roots and surtling cores in the woods' crypts; guck sacks, tar and muddy scrap
piles in the swamp's; icicles, ice shards, snowdrifts, crystals and hung deer in the mountains'; seeker creep,
hanging eggs, royal jelly and black cores in the mines; charred skulls, wall spikes and gems set into the walls in
the Ashlands; drifts, frost cores and frozen dead in the north. Ashlands walls have windows in their upper rows.

**Corridors are not all the same.** A long one may be closed midway by a gate under a band of wall, half
collapsed (rubble, a fallen pillar, the way open along one side), lined with bones (catacombs), carpeted with a
runner (keeps, temples, holds), hung with vines or edged with sharpened stakes. The biome adds its own: a muddy
scrap pile to dig through in the swamp, a barricade or seeker creep in the mines, icicles in the cold, spikes in
the Ashlands, a troll trap or a fuling trap.

**Pacing.** About one room in twelve of the plainer kinds is left nearly bare: one piece and a light in the dark.
Clutter never puts the same thing twice running, and some rooms are tidier than others.

**Floors.** With **Furniture** at **Furnished** or **Rich**, in a stone or grausten dungeon, the grand rooms
(halls, throne room, chapels, arena, treasury, entrance) get black marble tiles, and quarters, barracks, dens and
studies wooden boards, each half the time.

**Light by purpose.** Rooms are lit by standing torches along the walls, sconces on them, or a brazier or two in
the corners, in the biome's own lights. **Bright** puts a light every 5 m of wall, **Dim** every 8 m, **Dark**
every 12 m and only in the rooms that matter (and now and then another). The grand rooms (the entrance, the throne
room, the treasury, the feast hall, chapels, the arena) are lit closer; the rooms of the dead, the cells, the
torture chamber, dens, store rooms and hidden caches further apart, and never in the dark. In catacombs and
prisons some corridors stay dark between lit rooms.

**Deeper is richer and more dangerous.** Each level down holds a fifth of the top level's monsters and loot more:
the second level 1.2 times the top, the sixth twice, the sixteenth four times.

**Monsters.** A room gets about one monster for every 75 m² of floor at its kind's usual rate, times **Monsters**,
times its depth. They stand away from the way in, facing it. The key chest is guarded by one of the biome's
elites, tombs hold one, the throne room two, and a dead end's cache sometimes one (one star when **Monsters** is 2
or more). A corridor's niche now and then hides a foe. The **boss** is always placed once in
the arena, as the creature itself: no star at **Monsters** none, one star at × 1, two stars at × 2 or more.

**Loot.** Chests, treasure lying about, and the treasury's piles of coins and bars of the biome's metal grow with
**Loot** and depth. The key chest holds the crypt key and a few of the biome's valuables (coins, amber, rubies,
silver...), written into the chest so the game does not fill it with its own; a brazier stands beside it. The key
gate has a sign: *Sealed. The key lies within these halls.*

Changing **Light** or **Ruin** changes nothing else about the dungeon: lights and decay draw from random numbers of
their own.

### Why it holds 5000 m up

Every building piece needs support from the ground in game, and there is no ground up there. A dungeon of building
pieces stands on **black marble slabs** (8 × 8 m), the floor and the roof of every part in use: the one piece the
game never checks for support. Everything else stands on them, so it holds anywhere, 5000 m up as on the ground.
Things that stand on a floor are lifted 5 cm, because a piece whose centre is right on a surface gets no support
from it.

### Placing it in the world

![The generated dungeon's entrance on the ground](images/dungeon-entrance.jpg)
*The entrance placed by **Place in the world**: a burial chamber's mound on the ground, facing the camera.*

**Place in the world** puts the dungeon's **entrance** on the ground at the middle of the view, its door facing you,
and the dungeon 5000 m above it, where the game keeps its own:

- The entrance is the game's own location for the biome (see the table above). The editor writes it as the game
  writes a location, so the game makes its door, its stones and its way in whenever the zone loads, with the
  objects it makes around it. Its door leads up into the dungeon, and the door up there leads back out, as in
  the game's own dungeons.
- The dungeon is laid out where the game lays that entrance's own (a Frost Cave's goes to the middle of the zone),
  and turned so that a player coming through the door arrives in the entrance hall, facing into the dungeon.
- The **Plains** have no dungeon of their own: there a portal on the ground (3 m from the point) leads in and its
  twin in the entrance hall leads back, written linked under a tag such as `dg123`. Without an entrance in the
  way, the dungeon goes higher in 50 m steps if something is already up there.
- **One dungeon per zone** (64 × 64 m), as the game lays one out per zone: when something is already up there (a
  dungeon, or the space the game keeps above a dungeon entrance it has not laid out yet), the editor says so and
  places nothing. Put the entrance in another zone.
- Everything it places has no builder, like the game's own dungeons: the game takes it for a ruin. Raids ignore it,
  it is no base for fires, and taking it apart gives back a third of the materials.

The view stays at the entrance. **Go to the dungeon** (under the generator) takes it up, looking down into the
first level with the roofs cut away; **Go to the entrance** brings it back down. Like everything else in the
editor, it is one step of the history (`Ctrl+Z` takes it all back) until you **Save to world** or **Apply live**.

### Made of the game's own rooms

With **Made of** set to **The game's own rooms**, the editor joins the biome's dungeon rooms the game's way:

1. It picks the dungeon of the biome: a burial chamber in the Meadows and the Black Forest, a sunken crypt in the
   Swamp, a Frost Cave in the Mountains and the Deep North, an infested mine in the Mistlands, the Ashlands' and
   the Plains' own. A kind with no rooms to join (a camp, a village) becomes a Frost Cave, and the summary says so.
2. It starts with one of its entrance rooms, then adds rooms at open openings, weighted as the game weighs them,
   only where they do not overlap another and stay inside the dungeon's space: until there are about 8, 14, 20 or
   26 rooms (by **Size**).
3. It closes every open end with the game's end cap of that kind of opening.
4. It fills each room with what the game would put in it there (chests, spawners, torches, decorations), rolled
   from the room's place and the world's seed the way the game rolls them, and puts doors where rooms meet, by the
   game's own chance.
5. **Monsters** copies each spawner about that many times (none at none).

Placed in the world, this is one dungeon object holding its list of rooms, laid out from where the entrance puts
it, exactly as the game lays out its own. It then shows in the Dungeon panel's list, to edit room by room, and
**Go to the dungeon** goes inside it as **Go inside** does.

### Opening it in the Workshop

![A generated dungeon on the Workshop's plot, cut open](images/workshop-dungeon.jpg)
*A generated dungeon opened on the Workshop's plot, cut open at 3 m: its rooms, furniture and corridors seen from
above.*

**Open in the Workshop** writes the dungeon (made of building pieces) as a blueprint in Homestead's folder and
opens it in the [Workshop](workshop.md), to change it there: open a chest to change what it holds, move the
furniture, add your own touches, then **Save blueprint**. The blueprint:

- keeps what its objects hold (the key in its chest, the signs, the boss and its stars) and that it is a ruin;
- is tagged `dungeon` and `generated`, and its description gives the seed and the summary;
- has a **portal** in the entrance hall where players arrive. Pasted into a world from the Blueprints panel, it
  goes up behind an entrance as a generated dungeon does (see [Dungeons from blueprints](#dungeons-from-blueprints)).

A blueprint of that name already there is kept; the new one is numbered. In the Workshop itself, the Build panel's
**Library** has **Generate dungeon…**: the same choices (building pieces only), then **Open on the plot** or **Add
to the plot**.

Built in game with Homestead, the blueprint is only the pieces: no key in the chest, no boss.

## Dungeons from blueprints

Any blueprint can be a dungeon. Switch **A dungeon** on when you save it in the Workshop (the Blueprints panel's
**Details** has the same switch; it is the tag `dungeon`). Pasted into a world from the Blueprints panel, such a
blueprint is not put on the ground:

- The entrance of the dungeon of the biome under the click goes where you click, facing you, and the building goes
  5000 m above it, as **Place in the world** does for a generated dungeon.
- The Paste tool's turn and mirror apply (`R`, `F`, `,` `.`); its height does not: the dungeon's height is the
  game's.
- Put a **portal** (`portal_wood`) in the building where players should arrive, facing the way they should look:
  the entrance's door out opens there, and that portal is left out. Without one, players arrive at the building's
  middle, facing north.
- Where the biome has no dungeon of its own (the Plains), a linked portal pair stands in for the entrance: one on
  the ground, its twin where players arrive.

Pieces hold in the sky only on what the game never asks support for: make the ground of a dungeon of pieces with
black marble floors (`blackmarble_floor_large`), and stand everything else on them.

## Opening a dungeon

1. Open the area over a dungeon's entrance (the map's location markers show the entrances).
2. Pick **Dungeon** on the tool rail. The list under the generator shows the area's dungeons, each with its kind,
   its number of rooms and where it is (`Frost Cave, 34 rooms, at 1520, -2240`).
3. **Go inside** takes the view up to the middle of its rooms and cuts their roofs away (at 4 m above the floor
   when the cut was off), so you look down into them. **Go to entrance** takes the view back down to the dungeon's entrance on the ground,
   with nothing cut.
4. The cut slider (`Cut 4 m above the floor`) sets how high above the floor of the dungeon's entrance the cut is,
   from 0 to 30 m. Its right end, `Cut: off (roofs shown)`, shows the roofs again; its left end cuts at the floor.

Up there the camera no longer follows the ground: orbit, slide and fly around the rooms as usual. **Dungeon rooms**
in the View panel shows or hides them.

A dungeon nobody has been near yet has no rooms: the game lays it out the first time a player comes. The panel
says so (*The game has not laid this dungeon out yet (nobody has been near it).*); visit it in game, then open the
area again. Fuling villages and Meadows farms are laid out the same way, on the ground, and show in the list too.

## Adding rooms

Every room has **openings**, the doorways where it meets the next room. The game only joins openings of the same
kind: a Frost Cave has plain cave openings, ice openings, shrine openings and others. Some rooms (an ice corridor)
join one kind to another.

1. A green frame marks each **open end**: an opening no room meets yet.
2. Pick a room in the list under **ROOMS**: the rooms this kind of dungeon can take, the game's way (those of its
   themes). **Find a room…** narrows the list by name. End caps and entrance rooms are marked `(end cap)` and
   `(entrance)`.
3. Point at an open end (within 1.5 m of it): the room shows there, see-through, joined the way the game joins
   rooms. It is turned so that one of its openings of the same kind faces the open one, and moved so the two meet.
4. **Turn (R)** joins it by the next of its openings of that kind, when it has several.
5. Click to add it. It is selected, so **Delete room (Del)** takes it out again.

The line at the bottom of the panel counts the rooms, open ends and overlapping pairs
(`24 rooms, 3 open end(s). 1 overlapping pair(s).`). It warns, and the room's box turns red, when the room would
overlap another, or go past the dungeon's space (64 × 64 m around its zone's middle for most kinds). The game keeps
its own rooms clear of both; a list made here is built anyway, so it is only a warning.

With **With what the game puts in them** on (the default), a room comes with what the game would have made in it at
that place: chests, creatures' spawners, torches, braziers, ice, decorations. The editor rolls them as the game does
when it first generates a dungeon (from the world's seed and the room's place, each random part's chance, the
dungeon's theme and the room's height), so they are the ones the game would have chosen. Off: bare rooms.

**Close open ends** puts the game's end cap on every open end, so nothing leads out of the dungeon. For each open
end it takes the end cap of that kind of opening the game prefers. The message says how many open ends have no end
cap of their kind.

## Doors

Where two rooms meet, the game sometimes puts a door: a Frost Cave's cloth curtains and ice walls, a crypt's gates,
the mines' doors. Each kind of dungeon has its own, for each kind of opening.

- With **Doors where the game might put them** on (the default), an added room gets one where it joins, by the
  game's own chance, at the opening of the room that was there first, facing as it does.
- A square marks every place where two rooms meet and a door can stand: **blue** with a door, **orange** without.
  Click it to put the dungeon's door there, or to take the door away. Each click is one step of the history.

## Selecting and deleting

Click a room to select it (its box shows), then **Delete room (Del)**. What stands in the room (chests, spawners,
ice...) goes with it: every object inside its box that is nearer its middle than any other room's. The doors where
it met other rooms go too. The rooms next to it have open ends again: **Close open ends** caps them.

Each change rewrites the dungeon's list of rooms as one step of the history: `Ctrl+Z` puts things back. **Save to
world** writes it, or **Apply live** in live mode.

## Building in a dungeon

**Build here** gives the Workshop's [Build panel](workshop.md#the-build-panel) over the dungeon: pick a piece, then
click on a room's floor or wall, or on a piece already there. Pieces snap as with the game's hammer, and the cursor
goes through what the cut hides. Pick another tool to stop building.

In game every building piece needs support from the ground. A room's floors and walls are not pieces, so they
count as ground: pieces resting on them hold. Pieces hanging in the open, away from the rooms, break as soon as a
player comes near.

## Good to know

- Some of what the game puts in rooms (icicles, crystals, fish) settles on the nearest surface in game, so it may
  sit a little differently from where the editor shows it.
- The game draws dungeons dark, lit by their torches. The editor lights them like the outside, so you can see what
  you edit.
- Rooms come with the game's look, copied from your Valheim. When the editor is updated, the copy runs once more to
  add them.
- A generated dungeon is objects like any other: **Select** moves and deletes its pieces, and `Ctrl+Z` takes the
  whole placement back in one step.
- **Claude** can generate dungeons and edit dungeon rooms for you: see [Claude](claude.md).
