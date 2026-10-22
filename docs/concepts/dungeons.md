# Dungeons

Valheim keeps its dungeons in an unusual way: high in the sky, as a list of rooms the game builds when someone comes
near. Knowing this explains why a dungeon nobody has visited looks empty in the editor, how an edited dungeon needs no
mod, and how a dungeon of building pieces can hold 5000 m up. The [Dungeon](../dungeons.md) tool is built on it.

## How the game keeps dungeons

**Dungeons are not dug into the ground.** Each one is a **location** on the surface (a burial chamber's mound, a Frost
Cave's mouth) whose door teleports you to the dungeon itself, **5000 m above** it, where the game treats everything as
indoors. In the editor the camera stops following the ground up there, and **View → Dungeon rooms**
shows or hides the rooms.

```text
   5000 m  ┌───────────────┐  the dungeon's rooms
           │ ▢─▢─▢─▢       │  ◄── its door leads back down
           └───────────────┘
              ▲  teleport
   ground  ───┴─── the entrance (a location: mound, cave mouth, mine)
```

**A game dungeon is a list of rooms.** The game builds its dungeons from ready-made rooms. In the save, a dungeon is one
object holding its **list of rooms**: which room, where, turned which way. When a player comes near, the game builds
the rooms from that list, whatever it holds, without checking it against its own rules. That is why an edited dungeon
works in every player's game with no mod: the editor only changes the list.

**Until a player has been near, the list is empty.** The game lays a dungeon out the first time someone comes, so the
editor has no rooms to show or edit before that. Visit it in game, then open the area again. Fuling villages and
Meadows farms are laid out the same way, on the ground.

### Rooms, openings and end caps

![A Burial Chambers dungeon of the game's own rooms, cut open, orange and blue squares where rooms meet](../images/dungeon-rooms.jpg)
*A game dungeon 5000 m up, its roofs cut away: each room is one entry of the list. The squares mark where two rooms
meet: blue with a door, orange without.*

Every room has **openings**, the doorways where it meets the next one, and each opening has a kind: a Frost Cave has
plain cave openings, ice openings, shrine openings and others. The game only joins openings of the same kind, though
some rooms (an ice corridor) join one kind to another. An **end cap** is a small room that closes an opening, so
nothing leads out of the dungeon.

The game keeps its rooms from overlapping and inside the dungeon's space (for most kinds, 64 × 64 m round the middle
of its zone). A list the editor writes is built anyway, so the editor only warns when a room breaks those rules.

**What is in the rooms.** When the game first lays out a dungeon it rolls what goes in each room: chests, creatures'
spawners, torches, braziers, ice, decorations. Each part has its chance, from the world's seed, the room's place, the
dungeon's theme and the room's height. The editor rolls them the same way, so a room it adds comes with what the game
would have put there. A few things (icicles, crystals, fish) settle on the nearest surface in game, so they may sit a
little differently from where the editor shows them.

**Doors.** Where two rooms meet the game sometimes puts a door, by its own chance: a Frost Cave's cloth curtains and ice
walls, a crypt's gates, the mines' doors. Each kind of dungeon has its own for each kind of opening, and the door
stands at the opening of the room that was there first.

### Entrances, one per zone

The game lays out **one dungeon per zone**, and keeps the space above an entrance free even before the dungeon is laid
out. So the editor places a dungeon behind the game's entrance only where nothing is up there yet; otherwise put the
entrance in another zone. A Plains dungeon, reached by a portal instead, is stacked higher in 50 m steps when the space
is taken.

A placed dungeon's entrance is the game's own location for the biome, written as the game writes a location, so the
game makes its door, its stones and its way in each time the zone loads. The dungeon goes where the game lays that
entrance's own (a Frost Cave's to the middle of its zone), turned so that a player coming through the door arrives
facing in. The Plains have no dungeon of their own: there a pair of linked portals stands in for the entrance, one on
the ground, its twin where players arrive.

The game draws dungeons dark, lit only by their torches. The editor lights them like the outside, so you can see what
you edit.

## How the generator lays out a dungeon

The generator can make a dungeon of two things: **the game's own rooms**, or **building pieces**.

**With the game's own rooms**, it joins them the game's way. It picks the biome's dungeon (a burial chamber in the
Meadows and the Black Forest, a sunken crypt in the Swamp, a Frost Cave in the Mountains and the Deep North, an infested
mine in the Mistlands), starts with one of its entrance rooms, and adds rooms at open openings, weighted as the game
weighs them, where they fit, until it has about 8, 14, 20 or 26 rooms by size. It caps every open end, fills each room
as the game would, and puts doors where rooms meet by the game's chance. The result is one dungeon object with its list
of rooms, exactly like the game's own.

**With building pieces**, it plans the dungeon on a grid of 2 m tiles, one level at a time, then builds and furnishes
it. The same choices and seed always make the same dungeon. Each level is laid out in this order:

| | What is laid out |
|---|---|
| **Entrance hall** | Opens the first level. Players arrive before its back wall, the dungeon's name on a sign beside them. |
| **The way through** | About two rooms in five are the style's main rooms in turn (a crypt's great hall, crossing, gallery and chapel), each joined to the one before, mostly straight ahead. |
| **Down, or the boss** | Each level but the last ends in a stairwell down (8 × 24 m) to the level below, 6 m lower. The last ends in an antechamber, the boss's arena (24 × 24 m) and the treasury beyond it. |
| **Side rooms and wings** | Side rooms branch off, most often off crossings and long halls, picked by the style's weights. A side room often grows off the last one, and leans to go with it: a kitchen by the feast hall, cells by the guard room, a forge by the workshop. |
| **Twins** | Side rooms often get a mirrored twin across their hall (a temple nearly always, a goblin warren seldom), so the plan reads like a building, along axes. |
| **Ways round** | Rooms three or more apart along the way that happen to lie close are joined, so there is more than one way round. |
| **Dead ends and caches** | A corridor or two lead to nothing but a cache. One hidden cache (two past 20 rooms) lies off a side room behind a cracked wall or a tapestry. |
| **The key** | With the boss behind a gate, the key goes in a chest in the side room farthest from the arena. |
| **Doors** | The boss's gate takes the key; the treasury is behind a gate; cells get gates more often; grand rooms sometimes get grand double gates; the rest follow the style. Where stairs go down, an open doorway becomes a gate, so what lives below stays below. |

Rooms vary within this. Their **size** follows their kind (a storeroom can be 8 × 8 m, a great hall 12–20 m wide and
20–32 m long). Some have notched corners, an apse at the end, or a corner missing. **Corridors** run straight or bend,
and some rooms meet wall to wall. Rooms are 4 m high, but great halls, chapels, throne rooms and the arena may rise
10 m, through the level above. Long rooms may get **alcoves** off their sides.

### Why it holds 5000 m up

Every building piece needs support from the ground in game, and there is no ground up there. So a dungeon of building
pieces stands on **black marble floor slabs** (8 × 8 m), the floor and the roof of every part in use: the one piece the
game never checks for support (see [The support check](objects.md#the-support-check)). Everything else stands on
them. Things that stand on a floor are lifted 5 cm, because a piece whose centre is right on a surface gets no support
from it. To make your own dungeon of pieces, do the same: black marble floors, and everything on them.

Everything the generator places has no builder, as in the game's own dungeons, so the game takes it for a ruin (see
[Player-built pieces](objects.md#player-built-pieces)).

## Furnishing, light and guards

A dungeon of building pieces is dressed room by room. **Light** and **Ruin** draw random numbers of their own, so
changing them changes nothing else about the dungeon.

**Every room is furnished for its purpose**, in one of two to four ways picked by the seed:

| Room | Some of its furnishings |
|---|---|
| Halls | Colonnades with an altar; a mead hall round a fire with trestle tables; a hall of the dead with a sarcophagus on a dais; a bare hall of fallen pillars. |
| Chapels | Pews facing an altar between statues; a raised altar in a ring of braziers; a nave of rugs and candles. |
| Burial chambers, ossuaries, galleries | Rows of sarcophagi or one tomb on a dais; bones along the walls or heaped; niches, statues, cobwebs. |
| Barracks, store rooms, studies | Beds or bedrolls round a fire; stock along the walls or shelves in rows; desks and shelves. |
| Feast halls, throne rooms | Long tables, or a high table on a dais; colonnades, a war room, fallen columns round the dais. |
| Arenas, shrines | A ring of columns round the boss, a throne over skulls, or a pit sunk below ledges. |
| Kitchens, forges, workshops | The game's real stations: ovens, cauldrons, fermenters, smelters, the black forge. |

**More inside.** A grand stone room may carry a loft on posts over the way in; a burial chamber may hold an inner vault;
alcoves and niches hold a sarcophagus, a statue or a barrel. Corners get small groups that suit the room (a camp, a
meal, a scribe's desk, an armoury), and big rooms may get a **hero piece** found once in the whole dungeon: a dolmen, a
giant's skull, a wisp fountain. Each biome adds its own things (roots in the woods, guck and tar in the swamp, icicles
in the mountains, seeker creep in the mines, spikes in the Ashlands), and corridors vary: half collapsed, lined with
bones, carpeted, closed by a gate midway, or set with a trap. About one plain room in twelve is left nearly bare, one
piece and a light in the dark. With **Furniture** at Furnished or Rich, grand stone rooms get black marble tiles and
living quarters wooden boards, each half the time.

**Light by purpose.** Rooms are lit by torches, sconces or braziers in the biome's own lights: one every 5 m of wall
when **Bright**, 8 m when **Dim**, 12 m when **Dark** (and then only in the rooms that matter). Grand rooms are lit
closer; rooms of the dead, cells, store rooms and caches further apart. In catacombs and prisons some corridors stay
dark between lit rooms.

**Deeper is richer and more dangerous.** Each level down holds a fifth more of the top level's monsters and loot: the
second level 1.2 times the top, the sixth twice, the sixteenth four times.

**Guards.** A room gets about one monster for every 75 m² of floor at its kind's usual rate, times **Monsters**, times
its depth, standing away from the way in and facing it. The key chest is guarded by one of the biome's elites, tombs
hold one, the throne room two. The **boss** is always placed once in the arena, as the creature itself: no star at
**Monsters** none, one star at × 1, two stars at × 2 or more. With **Monsters come back** on, the others come from the
game's spawners and return a while after being killed (see [Spawn points](objects.md#spawn-points)); the game's own
rooms always bring spawners.

**Loot.** Chests, treasure lying about and the treasury's coins and bars of the biome's metal grow with **Loot** and
depth. The key chest holds the crypt key and a few valuables, written into the chest so that the game does not fill it
with its own loot, and the key's gate carries a sign: *Sealed. The key lies within these halls.*

Built in game with Homestead, a dungeon blueprint is only its pieces: no key in the chest, no boss.
