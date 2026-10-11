# A new world

The start page's fifth card, **A new world**, makes a new Valheim world and helps you choose its
seed. The world is a normal one, made by the game's own rules from its seed: everyone can join it
without any mod, console players on crossplay included.

## Seeing a seed

Type a seed (up to 10 letters and digits) or press **Roll** for a random one. The map on the right
shows that seed's world as the game will make it, from the editor's copy of the game's world
generator: land coloured by biome, sea shaded by depth, north up. The mouse wheel zooms where the
pointer is, dragging moves the map, and a double click (or the bottom right buttons) shows all of it
again; zoomed in, the part in view is drawn again from the generator, so coasts and rivers stay sharp.

Over it, the places the game will lay out when the world first loads, found by following its own
placement rules:

- the **start** (red dot): the start temple;
- the **bosses' altars** (purple): Eikthyr, the Elder, Bonemass, Moder, Yagluth, the Queen, Fader;
- the **traders' spots**: Haldor (yellow), the Bog Witch (green) and Hildir (pink). The game lays out
  several spots for each trader; the first one players come near becomes the trader's camp and the
  others vanish.

Hover a mark for its coordinates and its distance from the start. The switches in the map's corner
(**On the map**) show or hide the start, the bosses and each trader.

The **Dungeons** switches show where each kind of dungeon will be: burial chambers, troll caves,
sunken crypts, frost caves, infested mines, the Ashlands' charred fortresses and Hildir's dungeons.
They are off at first; the first one switched on looks for the seed's dungeons (a few seconds: the
game lays out its smaller places before them), and they are kept for the seed. The **Dungeons** list
then gives, for each kind, the nearest from the start, how many there are and how many lie within
2 km. Burial chambers, troll caves and frost caves are close to where the game puts them, but some may
be elsewhere (the game's alt biomes move a few). These are exactly where the game puts them. (The smaller places, villages,
ruins and the like, are not shown: some of them depend on an alt biome, such as Dark Meadows, that the
game picks at random each time it loads a world.)

Beside the map, the seed's world as lists (click a trader's or boss's row to see the nearest on the
map):

- **Land**: how much of the world is land, how big the start's landmass is, and the largest one.
- **Land around the start**: how much of the ground within 400 m of the start is land.
- **Biomes**: each biome's share of the land.
- **Nearest to the start**: how far the first Black Forest, Swamp, Mountain, Plains and so on are,
  and whether you need to sail there (**by sea**).
- **Traders** and **Bosses**: how far the nearest spot of each trader, and the nearest altar of each
  boss, are from the start.

Land here is ground you can stand on or wade through. The game names much of the shallow sea after
the land near it; the map counts it as sea.

## Finding a seed

**Find a seed** tries many random seeds and keeps the best for what you ask:

- **The start on a large landmass** and **Land all around the start**.
- **Each biome reachable on foot from the start**: no sailing for the early biomes.
- **Swamp / Mountain / Plains within** so many metres of the start (0: no wish).
- **Haldor / Bog Witch / Hildir within**: one of the trader's spots within so many metres of the
  start.
- **Bosses 1–5 within**: an altar of each of Eikthyr, the Elder, Bonemass, Moder and Yagluth within
  so many metres.

Looking for traders or bosses makes a search a few times slower: the game's placement maps the whole
world for each seed.

Choose how many seeds to try (60, 150 or 400) and press **Find seeds**. The best appear as small
maps with their score; click one to see it large. **Stop** keeps what was found so far. A search
looks at each seed more coarsely than the large map, so small islands may be missed.

## Creating the world

Give the world a name, pick the save folder (**Saved in**: your local worlds, or a Steam cloud
folder) and press **Create world**. The editor writes the world's files and opens it.

The game lays out the start temple, traders, bosses and dungeons the first time it loads the world,
and makes each area as players reach it. The editor can only reshape areas the game has made, so
play the world (or walk around) before editing a part of it.
