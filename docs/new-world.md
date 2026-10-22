# A new world

![A seed's world on the New world page: land coloured by biome, the start, the bosses' altars and the traders' spots marked, the seed's facts in the column on the right](images/new-world.jpg)

*The red dot is the start, purple diamonds are bosses' altars, the yellow, green and pink dots are the traders' spots. The column on the right lists the seed's facts.*

**A new world** makes a new Valheim world and helps you choose its seed. You see a seed's world before it
exists: its biomes, where the start, the bosses and the traders will be, and where its dungeons are. You can let
the editor look through many seeds for one that suits you. The world you create is a normal one, made by the
game's own rules from its seed: everyone can join it without any mod, console players on crossplay included.

## How to use it

1. On the start page, choose **A new world** under **Create** in the side menu, then **Make a new world**.
2. The page opens with a random seed already shown. Type a seed and press **Generate world** (or `Enter`), or
   press **Roll** for another random one. Or let **Find a seed** look for you (below).
3. Look at the map and the facts beside it. Switch on the kinds of dungeon you care about.
4. When you like the seed, give the world a **Name**, choose where it is **Saved in**, and press **Create
   world**. The editor writes the world and opens its map. The folder offered first is the one the installed
   game saves to (on Linux, the Windows build run through Proton keeps its worlds inside its Steam prefix),
   so the game lists the world; the other world folders found come after it.

**Start** (top left) goes back to the start page; nothing is made.

## Seeing a seed

| Control | What it does |
|---|---|
| **Seed** | Up to 10 letters and digits, as in the game. The same seed always makes the same world. |
| **Roll** | Puts another random seed (10 letters and digits) in the box and shows its world at once. |
| **Generate world** | Shows the seed's world (or press `Enter` in the seed box). Greyed while the seed in the box is the one shown. |

The map shows the seed's world as the game will make it, worked out with the editor's copy of the game's world
generator: land coloured by biome (higher ground lighter), water on land tinted blue, the sea shaded by depth,
north up. It takes a few seconds; a box over the map says "Looking at …'s world…" meanwhile.

- The **mouse wheel** zooms where the pointer is, **dragging** moves the map, and a **double click** shows the
  whole world again. The buttons at the bottom right do the same: **+** closer, **−** farther, **⤢** all of the
  world.
- Zoomed in, the part in view is drawn again from the generator at one point per screen pixel, so coasts and
  rivers stay sharp.
- The bottom left corner shows the coordinates under the pointer.

### The start, the bosses and the traders

Over the map are the places the game lays out first when the world loads for the first time, found by following
its own placement rules:

- the **start** (red dot with a white ring): the start temple, where new players arrive;
- the **bosses' altars** (purple diamonds): Eikthyr, the Elder, Bonemass, Moder, Yagluth, the Queen and Fader;
- the **traders' spots**: Haldor (yellow), the Bog Witch (green) and Hildir (pink). The game lays out several
  spots for each trader; the first one players come near becomes the trader's camp and the others vanish.

Hover a mark for its coordinates and its distance from the start. The switches in the map's corner, under **On
the map**, show or hide **The start**, **Bosses' altars**, **Haldor**, **Bog Witch** and **Hildir**.

These spots are exactly where the game puts them: the game picks them with a random number made from the seed
and the kind of place, so the editor, replaying the same rules, gets the same answer. The rules (where each kind
may go: biome, height, slope, distance from the middle and from others of its kind) are read from your Valheim's
files the first time, which takes a few seconds once.

### Dungeons

![The same seed with four kinds of dungeon shown as small coloured squares](images/new-world-dungeons.jpg)

*Each small square is one dungeon; the colour says its kind, as in the switches and the list.*

The **Dungeons** switches show where each kind of dungeon will be: **Burial chambers**, **Troll caves**,
**Sunken crypts**, **Frost caves**, **Infested mines**, **Charred fortresses** (Ashlands) and **Hildir's
dungeons**. They are off at first. The editor starts looking for them as soon as a seed is shown, out of
sight: to find them it lays out everything the game lays out before them (the smaller places too), which takes a
few seconds. Switch a kind on and they show as soon as they are found. The dungeons of the last 20 seeds you
looked at are kept, so going back to a seed shows them at once.

The **Dungeons (the nearest)** list then gives, for each kind, how far the nearest one is from the start, its
biome, how many there are in the world and how many lie within 2 km. Click a row to switch that kind on and see
the nearest on the map.

They are where the game puts them. That includes the kinds an **alt biome** (a variant such as Dark Meadows
or Rock Black Forest) adds or keeps out: the game picks the areas that get one from the seed, and the editor
picks them the same way. Villages, ruins and the other small places are not shown.

### The seed's facts

Beside the map, the seed's world as lists. Click a row with a mark (the start, a trader, a boss, a dungeon) to see
it, or the nearest of its kind, on the map.

- **The world**: **Land** (how much of the world is land), **The start's landmass** and **The largest landmass**
  (each as a share of all the land), **Land around the start** (how much of the ground within 400 m of the start
  is land), and **The start** itself: its coordinates, "where the game will put its start temple".
- **Biomes (the nearest from the start)**: each biome's share of the land, and how far its nearest land is from
  the start, with "by sea" when it is on another landmass (you need a boat, or to cross water, to get there).
- **Traders (the nearest spot)**: how far the nearest spot of each trader is, and how many spots it has.
- **Bosses (the nearest altar)**: how far the nearest altar of each boss is, and how many there are.
- **Dungeons (the nearest)**: see above.

Land here is ground you can stand on or wade through: at most 1 m under water, or 3 m in a Swamp, whose pools are
part of it. The game names much of the shallow sea after the land near it; here it counts as sea. A landmass is
land joined to land without crossing deeper water.

## Find a seed

![Find a seed: the best seeds found, each as a small map with its seed and score](images/new-world-find.jpg)

*The best seeds, best first. Hover one for its facts; click it to see it on the large map.*

**Find a seed** looks at many random seeds with the game's world generator, in the background, and keeps the
best for what you ask. 0 in a distance box means "no wish".

| Control | What it does |
|---|---|
| **The start on a large landmass** | Prefer seeds whose start is on a large landmass (on at first). |
| **Land all around the start** | Prefer seeds with little water within 400 m of the start (on at first). |
| **Each biome reachable on foot from the start** | Prefer seeds where the nearest of each biome is on the start's own landmass: no boat needed. |
| **Swamp within** / **Mountain within** / **Plains within** | Prefer seeds where that biome's nearest land is within so many metres of the start (0 to 8,000 m, in steps of 250; 1,500 for the Swamp and 2,000 for the Mountain at first). |
| **Haldor within** / **Bog Witch within** / **Hildir within** | Prefer seeds where one of that trader's spots is within so many metres. |
| **Bosses 1–5 within** | Prefer seeds where an altar of each of Eikthyr, the Elder, Bonemass, Moder and Yagluth is within so many metres. |
| Seeds list | How many random seeds to look at: **60 seeds**, **150 seeds** (the default) or **400 seeds**. |
| **Find seeds** | Starts the search; the bar under it shows how far it is. |
| **Stop** | Stops the search and shows the best found so far. |

The 12 best seeds appear under **Found seeds**, best first, each as a small map with its seed and score. Hover
one for its facts in a few lines; click it to see it on the large map (it gets a coloured border).

### How seeds are scored

Each wish adds to a seed's score:

- **The start on a large landmass** adds up to 3 (the start's landmass's share of the land, times 3);
  **Land all around the start** adds up to 2 (the share of land within 400 m, times 2).
- A distance wish that is met adds 1 to 2: more the closer it is. One that is missed takes away, more the further
  it is; a biome or trader the seed does not have at all takes away 2. **Bosses 1–5 within** counts the
  average of the five bosses.
- **Each biome reachable on foot** adds 0.25 for each biome whose nearest land is on the start's landmass.

A score is never below 0. The search looks at each seed more coarsely than the large map (points about 220 m
apart instead of about 40 m), so small islands and narrow straits may be missed: look at a found seed on the
large map before you choose it. Looking for traders or bosses makes the search a few times slower, because each
seed's places must be laid out too.

## Creating the world

| Control | What it does |
|---|---|
| **Name** | The world's name, as the game lists it. It is also its folder's name, so it cannot hold `/ \ : * ? " < > \|`. |
| **Saved in** | Which of the game's world folders it goes in: your local worlds (`worlds_local`) or the Steam cloud worlds folder (`worlds`), those that exist on this computer (on Linux also Valheim's folder when it runs through Proton). The game lists the worlds of its own folders. |
| **Create world** | Checks the name and seed, makes the world and opens its map. A world of the same name already in that folder is never written over: you are asked for another name. |

### What it writes

The editor writes a new folder named after the world, holding save number 1 of an empty world in the save format
of Valheim 1.0.20 and later (format 42, as the game writes a new world; an older Valheim refuses it, so update the
game or the server first):

- the world's details: its name, its seed and the version of the game's world generator;
- an empty zone list, marked as "locations not laid out yet";
- an empty list of objects, and last a small file that marks the save as complete (as the game does, so a save cut
  short is never taken for a good one).

There are no trees, rocks or buildings in it yet. The game does the rest the first time it loads the world: it
lays out the start temple, the traders, the bosses' altars, the dungeons and the other places (exactly where this
page showed them), and then makes each zone, with its vegetation, as players come near it.

### After creating

The editor can only reshape places the game has made. So start the game, load the world and play it (or walk
around) before you edit a part of it; then open it again in the editor. The message after creating says so too.
Until then the map shows the seed's land with nothing on it.

## Good to know

- The seed is the only thing that decides the world: a world created here is the same as one the game would
  create from the same seed.
- A seed name is turned into a number the same way the game does it, so letter case matters: `Valheim` and
  `valheim` are two different worlds.
- If the bosses and traders cannot be read from your game's files, the page says so: the map still shows the
  biomes, but the marks are missing and the search cannot look for them. The log (**Open log** at the bottom of the start
  page's side menu) says why.
- With the Expand World add-on installed and on (Settings → Add-ons), the seeds looked at and the world made follow the mods' settings
  and data files (the page says so above Create): a wider world, other biomes and places. See
  the [Expand World add-on](../addons/ExpandWorld/README.md). A biome the mods' biome file adds and a territory show on the map in their own
  colours, and the water line is the mods' sea level.
- With an add-on for a world generation mod in use (the [Riverheim add-on](../addons/Riverheim/README.md)) and the
  mod installed, a **Generator** choice appears under Saved
  in: Riverheim (chosen at first, as the game makes every new world a Riverheim world while the mod is installed) or
  the game's. With Riverheim the map, the marks and the search show Riverheim's world, the world is made as a
  Riverheim world, and each seed takes a few seconds: your Riverheim lays its whole world out first.
