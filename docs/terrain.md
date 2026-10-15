# Ground tools

![A mound raised with Raise, the brush ring next to it and the Raise panel](images/sculpt.jpg)
*A mound made with a few Raise strokes. The white ring is the brush. The panel on the left holds Size, Strength,
Shape, Falloff, Load stamp… and No limit.*

The ground tools shape and paint the ground of the open area. They work like the game's hoe, pickaxe and
cultivator, but with a brush you hold and drag, shapes you put in with one click, and whole mountains. Every change
keeps to the game's own rules, so the world loads in every player's game (console players too) with no mod. Each
stroke or click is one step in undo (`Ctrl+Z`) and History, and all of them work with the [Mask](masks.md).

## How to use a brush

1. Choose a tool in the rail's **SCULPT** or **PAINT** section, or press its key (below).
2. Set **Size** (`[` and `]`), **Strength**, **Shape** and **Falloff** in the panel.
3. Hold the left mouse button on the ground and move. The tool keeps working while the button is down, so holding
   still in one place works more and more. The middle button still slides the view.
4. Let go. The whole stroke becomes one undo step, named after the tool.

The ring on the ground follows the hills under it and shows the brush's shape. A square brush or a stamp shows its
turned outline. The Ring shape shows a second, inner outline.

## Sculpt tools

| Tool (rail label) | Key | What it does |
|---|---|---|
| **Raise** | `1` | Lifts the ground under the brush while you hold the button. |
| **Lower** | `2` | Digs the ground down. |
| **Flatten** | `3` | Levels the ground to one height: the height where the stroke starts, or a fixed height. |
| **Smooth** | `4` | Evens out bumps and sharp edges. |
| **Naturalize** (rail: **Natural**) | `0` | Turns flat, tool-made ground into natural-looking bumps. |
| **Erode** | `O` | Weathers the ground: steep slopes slide down (Thermal), or rain cuts gullies (Water). |
| **Restore** | `5` | Brings the ground back to how the world generated it, and removes paint. |

How fast they work: at **Strength** 1, Raise and Lower move the middle of the brush by up to 6 m per second (2.4 m
per second at the default 0.40). Flatten, Smooth, Naturalize and Restore do not add a fixed amount. Each moment they
move every point a share of the way towards their aim, so the ground settles on the aim and never overshoots it.

- **Flatten** pulls each point towards the target height. Lower ground around it is raised and higher ground is
  cut, so you get a flat pad.
- **Smooth** moves each point towards the average of itself and its 8 neighbours. Hold it longer, or go over the
  same place again, for a stronger effect.
- **Naturalize** first works out the ground's broad shape (the ground blurred over a few metres, as it was when the
  stroke started). It adds bumps from a noise pattern to that, and pulls the ground towards the result. So a second
  stroke over the same place does not pile up more and more bumps: it only gets closer to the same aim.
- **Restore** takes edits away gradually: each moment it removes a share of the change, and a point within 1 cm of
  its generated height becomes unchanged again. It also takes back a [No limit](#no-limit) lift that is not saved
  yet. It removes paint only in the inner part of the brush (where the brush works at more than half strength), so
  the paint's edge is not frayed.

![The same Flatten stroke started on top of a mound levels its top](images/flatten.jpg)
*A big mound whose top was levelled by one Flatten stroke started on it. **Level to the height where the stroke
starts** is on, so the **Height (m)** box is greyed and shows the height it took: 42.4 m.*

### Brush settings

| Control | What it does |
|---|---|
| **Size** | The brush radius, 1 to 30 m (for a square, half its side). `[` and `]` change it by 1 m. |
| **Strength** | How fast the brush works while you hold the button, 0.05 to 1 (default 0.40). |
| **Shape** | **Circle**, **Square**, **Ring**, **Ragged (noise)**, or a picture (**Stamp: …**, see [Stamps](#stamps)). Remembered. |
| **Falloff** | How the effect fades from the middle to the edge (below). Hidden for stamps: a picture has its own fade. Remembered. |
| **Turn** | Only for **Square** and stamps: turns the brush, -180° to 180°. |
| **Load stamp…** / **Forget stamp** | See [Stamps](#stamps). |
| **No limit: past the game's ±8 m** | See [No limit](#no-limit). Off each time the editor starts. |

The shapes:

| Shape | What it covers |
|---|---|
| **Circle** | A round brush. |
| **Square** | A square as wide as the brush. It turns with **Turn**. |
| **Ring** | Only a band: nothing inside 40% of the size, full strength at 70%, nothing at the edge. Good for crater rims, moats and earth walls. The inner outline shows where the band starts. |
| **Ragged (noise)** | A round brush whose edge comes and goes by up to about a third of the size. The noise is fixed to the world, so strokes get uneven, natural edges, and two strokes over the same place have the same edge. |

![Five dabs of paved paint, one per brush shape](images/brush-shapes.jpg)
*From left: Circle, Square, Ring, Ragged (noise) and the Crater rim stamp.*

The falloffs, from the middle of the brush (full strength) to its edge:

| Falloff | How it fades | Good for |
|---|---|---|
| **Smooth** (default) | An S-curve: soft in the middle and at the edge. | Most work. |
| **Linear** | Evenly from the middle to the edge. | Cones, even slopes. |
| **Dome** | Stays strong far out, then drops fast near the edge. | Round hills with steep feet. |
| **Flat top** | Full strength over the inner 80%, fading only in the outer fifth. | Pads, terraces, flat-topped hills. |
| **Peak** | Strong only near the middle. | Spires and pits. |
| **Sharp edge (pickaxe)** | Full strength right to the edge, nothing beyond. | Steep walls and straight cuts, like the game's pickaxe. |

![Five mounds raised by the same stroke, one per falloff](images/brush-falloffs.jpg)
*Side view of the same stroke with each falloff. From left: Smooth, Dome, Flat top, Peak, Sharp edge (pickaxe).*

### Flatten settings

| Control | What it does |
|---|---|
| **Level to the height where the stroke starts** | On (default): the ground where you press the button sets the height for the whole stroke. The **Height (m)** box shows the height taken. |
| **Height (m)** | Used when the option above is off: every stroke levels to this height. |

**Alt + click** the ground with any sculpt or paint brush picks the height there: the editor switches to
**Flatten**, turns the option above off and puts that height in **Height (m)**. This is the easy way to make several
pads match, or to level a building site to the height of a door you already built.

### Naturalize settings

| Control | What it does |
|---|---|
| **Bumps** | How tall the bumps are, 0.2 to 4 m (default 1.2 m). |
| **Bump size** | How wide they are, 4 to 60 m (default 16 m): small values give rough ground, large values gentle swells. It also sets how far the ground is blurred for the broad shape. |
| **New pattern** | A new random bump pattern for the next Naturalize stroke and for natural paths. |

The pattern is fixed to the world, not to the brush, so neighbouring strokes and areas join without seams. The
[Path](path.md) tool's natural look and the [Area](area.md#ground) tool's Naturalize use the same **Bumps**, **Bump
size** and pattern.

![Levelled ground, half of it naturalized](images/naturalize.jpg)
*Levelled ground with slope colours on. The naturalized half shows its bumps as coloured patches; the flat half
stays plain green.*

### Erode settings

| Control | What it does |
|---|---|
| **Thermal** | Where the ground between two neighbouring points is steeper than the **Rest angle**, material slides from the higher point to its lower neighbours, shared out by how much too steep each one is. It moves half of the extra each time, so slopes settle instead of swinging. Ground is only moved, not lost: screes below cliffs, softened tool-made walls. |
| **Water** | Rain. Drops fall on random points under the brush and run downhill for up to 40 steps. A drop picks up ground where it runs fast down a slope. It drops what it carries where it slows down, or to fill a pit it runs into, and it slowly dries up. Many drops make gullies down slopes, fans of material at their feet and smoother valley floors. |
| **Rest angle** | Thermal only: the steepest slope that stays put, 10° to 60° (default 33°). Lower values flatten more. |

Hold and drag like any brush. **Strength** sets how fast it works (with Water, how many drops fall); the shape,
falloff and Mask set where it may change the ground. Points just outside the brush may receive material that slides
or washes out of it. The [Area](area.md#ground) tool's **Erode** action does both kinds over a whole selection at
once: thermal, then a lot of rain, then thermal again to settle.

![Two steep mounds, the right one weathered by Erode (Water)](images/erode.jpg)
*Left: a mound as it was raised, steep and smooth-sided. Right: the same kind of mound after Erode (Water), with
gullies, ragged flanks and material spread at its foot.*

## Stamps

A stamp is a picture used as the brush shape: white parts work fully, grey parts partly, black parts not at all.
Choose one in **Shape**, where stamps are listed as **Stamp: …**. A stamp covers a square twice as wide as the
**Size**, and turns with **Turn**. Without **Stamp once** a stamp works like any brush shape, with every sculpt and
paint tool.

| Stamp | Shape |
|---|---|
| **Mountain** | A rough peak. |
| **Mesa (flat top)** | A flat top with steep, slightly ragged sides. |
| **Crater rim** | A ring wall, for craters (Lower inside it afterwards) and old earthworks. |
| **Dunes** | Parallel ridges inside a round patch. |
| **Rocky ground** | Lumpy, broken ground. |

| Control | What it does |
|---|---|
| **Load stamp…** | Uses any picture (PNG, JPEG...) as a stamp: a heightmap from another tool, a logo, a hand-drawn shape. Its brightness is the strength, and transparent parts count as black. It is shrunk to 128 × 128 points and kept for next time under its file name (in the editor's data folder, `stamps.json`). |
| **Forget stamp** | Removes the loaded picture chosen as **Shape**. Only shown for loaded pictures: the built-in stamps stay. |
| **Stamp once: a click puts the whole stamp in** | Only with Raise or Lower and a stamp as Shape. One click puts the whole picture into the ground at once, instead of painting while the button is held. |
| **Height** | With Stamp once: how high (Raise) or deep (Lower) the white parts go, in metres (default 4). |

A Stamp once click is one undo step named after the stamp ("Stamp: Mesa (flat top)"). The Mask and the ±8 m limit
apply (unless No limit is on).

![The five built-in stamps, each put in once](images/stamps.jpg)
*Each built-in stamp put in with Stamp once. Front row from left: Mountain (the lowest, faint), Mesa (flat top),
Crater rim. Back row from left: Dunes, Rocky ground.*

## Paint tools

| Tool (rail label) | Key | What it does |
|---|---|---|
| **Dirt** | `6` | Bare dirt, like a trodden path. |
| **Cultivate** | `7` | Cultivated soil, as the cultivator makes. In game, crops can only be planted on it. |
| **Paved** | `8` | Paved stone ground. |
| **Clear paint** (rail: **Clear**) | `9` | Wipes paint: plain ground of the biome. |

Paint uses the same Size, Strength, Shape, Falloff and Mask as the sculpt brushes. It changes how the ground looks
and what grows there, never its height.

**How paint is stored.** As in the game, each ground point (one per metre) has a paint value, with a share of dirt,
of cultivated and of paved, and a mark saying it was painted. A point that was never painted shows the paint the
world generated. A paint brush moves the point's shares towards its own colour a little each moment. So a light
stroke, or the brush's soft edge, gives a blend, and holding longer gives the full colour. The first time a point is
painted, it starts from plain ground.

**Clear paint or Restore?** **Clear paint** paints plain biome ground: the point stays painted, so it also hides any
dirt the world generated there. **Restore** removes the paint mark, so the point shows what the world generated
again.

![Stripes of paved, cultivated and dirt ground with a band wiped by Clear paint](images/paint.jpg)
*From back to front: paved, cultivated and dirt stripes. A band across all three was wiped by Clear paint (its
panel on the left).*

## How the ground works

**One height per metre.** The ground has one height point per metre: 65 × 65 points per 64 m zone (neighbouring
zones share their edge points). So a wall always spans about a metre. It cannot be more vertical than that, in the
editor or in game, and the ground can never overhang (caves are made with rocks, see [Cave](path.md#cave)). Each
square metre is drawn as two triangles split along the same diagonal as in game (south-east to north-west corner).
An edge running north-west to south-east comes out clean; one running north-east to south-west shows a small
sawtooth, in game too.

**The ±8 m limit.** The game stores ground edits as a change from the generated ground, at most ±8 m per point (plus
a smoothing change of at most ±1 m, which the game's hoe makes). It throws away anything further. The editor keeps
to the same rule. A point that reaches the limit stops there and is tinted red (View panel: **Limit marks**, on by
default). When you let go, the status bar says "Reached the game limit: ground can only move 8 m from its original
height (red points)." [No limit](#no-limit) goes past it.

**The status bar** shows, under the cursor, the ground height, its original height and the change, for example
`ground 41.20 m (original 35.00, +6.20)`. It adds "at the ±8 m limit" when the point is there. Heights are the
game's: the sea is at 30 m.

**The locked edge.** The outermost line of points of the open area never changes, so the area always joins the
ground next to it without a step. To edit there, move the area with the top-bar arrows, make it bigger with the area
size list, or turn on **Follow**.

**Locations.** Villages, the trader, dungeon entrances and similar places level the ground around them while the
game runs. The editor shows the ground with that levelling already done. A note at the bottom left counts the
locations in the area, and **Location flattening** in the View panel draws where they level (a ring for the flat
part, a fainter one where it blends in). Your edits are added on top of that levelled ground.

**Saving.** A stroke, a Shape, Stamp once or Mountain click, and a Path **Apply** are each one undo step. Nothing is
written to the world until you **Save** (or **Apply live** in [live mode](live-mode.md)).

## No limit

![Two stacks raised by the same stroke, one stopped at 8 m, the other with No limit](images/no-limit.jpg)
*The same long Raise stroke twice. Left: stopped at 8 m, its top tinted red. Right: a spire raised with No limit,
far higher.*

The **No limit: past the game's ±8 m** switch, at the bottom of the brush options (and under the
[Area](area.md#ground) tool's Flatten, Raise and Lower: it is the same switch), lets the ground tools move the
ground as far as you like, up to 400 m from the generated ground: mountains, cliffs, canyons, deep caves. It works
with every sculpt brush, Stamp once, Shape, the Path tool (Cave too) and the Area tool. It is off each time the
editor starts. The [Mountain](#mountain) tool and pasting a copied place always go past the limit, whatever the
switch says.

While you work, the part past the ±8 m is kept as a separate "lift" on top of the generated ground. Ctrl+Z, History
and **Restore** treat it like any other change.

### How it is saved: ground discs

The game never stores ground edits more than 8 m from the generated ground. But it does level the ground under its
locations without that limit, and it counts that levelled ground as generated. The editor uses this. When you
**Save** (or **Apply live**), it turns ground past the limit into **ground discs**: invisible locations of the game's
own. A disc has no model. It only levels a circle of 8 m radius to its height and blends the ground around it out to
about 13 m.

1. Discs go on a 6 m grid, only where your ground is more than 5 m away from what the ground would be without them.
2. The editor works out the ground exactly as the game will build it from the discs, in the game's order, and
   adjusts the disc heights over several rounds to come as close as possible to your shape. Where the ground is still
   too far off, it adds a disc and tries again.
3. Ordinary ground edits (within ±8 m of the new ground) make up the rest.

Every player's game builds the same ground from them, console players included, with no mod.

What this means for you:

- **The save message** says how many discs were made (and how many old ones they replace), how many trees and rocks
  were taken away, and how far off the steepest ground still is. For example: "No limit ground: 312 ground disc(s),
  40 tree(s)/rock(s) taken away; some ground is up to 2.5 m off (too steep)".
- **History restarts.** After saving No limit ground, the area is read again from the new world and the history is
  cleared, because the ground under it is not the same any more. Check the shape before you save.
- **The new ground counts as generated.** After saving, the status bar's "original" height is the disc ground. The
  game's hoe and pickaxe work ±8 m from it, and so do the editor's tools. **Restore** brings ground back to the disc
  ground, not to the world's first ground. To take a saved shape away, work it down again with No limit on: discs
  near new lift are worked out again on the next save.
- **Trees, rocks, ore, bushes and pickables** where the ground moved more than 2 m are taken away on saving (they
  would be buried or float). Building pieces stay.
- **Dirt.** The game paints a disc's ground as dirt. The editor paints those points back with the biome's own ground,
  so the discs do not show.
- **Very steep walls** come out a little softer than drawn: a disc cannot follow a cut narrower than itself.
- **Zones the game has not generated yet** keep their old ground: their lift is dropped, and the message says how
  many. Visit them in game first.
- **The map** in game is drawn from the world's seed, so it does not show the new shape.
- **From afar**, the shape appears when you come near, as a village's levelled ground does.
- The editor hides the discs: they are not shown as locations, markers or objects.

## Shape (`G`)

![Shape presets placed side by side](images/shape.jpg)
*Front row from left: Mound, Cone, Mesa. Back row from left: Crater, Moat (ring ditch), Ridged hill. The Shape panel
on the left shows the Mound preset and its formula `h * smooth(1 - d / r)`.*

Like WorldEdit's `//generate`: a click puts a whole shape into the ground, centred where you click. The ring under
the pointer shows its radius. Each click is one undo step ("Shape: Mound").

| Control | What it does |
|---|---|
| **Shape** | **Mound**, **Cone**, **Mesa**, **Crater**, **Moat (ring ditch)**, **Bowl**, **Ridged hill**, or **Formula…** for your own. Each preset is a formula you can change: changing it switches the list to **Formula…**, and your formula is remembered. |
| **Radius** | 2 to 80 m (default 16 m). Only the ground within it changes. |
| **Height** | How high it rises, or how deep craters, moats and bowls dig (m, default 5). It is `h` in the formula. |
| **Formula** | How many metres to add to the ground at each point; a negative number digs. A mistake is shown in orange under it, and the shape cannot be placed until it is fixed. |

The formula is worked out for every ground point within the radius, and the result is added to the ground there.
In it:

| Name | Meaning |
|---|---|
| `x`, `z` | Metres east and north of the click. |
| `d`, `r`, `h` | Distance from the click, the radius, the height. |
| `n(x, z)` | Smooth noise from about -1 to 1, from the brushes' pattern (**New pattern** in Naturalize changes it). Use `n(x / 10, z / 10)` for wide bumps. |
| `pi` | 3.14159… |
| Functions | `abs`, `atan2(y, x)`, `bell(t)` (1 at 0, fading out by about ±1), `ceil`, `clamp(v, a, b)`, `cos`, `exp`, `floor`, `log`, `max(…)` and `min(…)` (any number of values), `pow(a, b)`, `round`, `sign`, `sin`, `smooth(t)` (0 below 0, 1 above 1, a smooth S between), `sqrt`, `tan`. |
| Operators | `+ - * / %`, `^` (power), comparisons `< > <= >= == !=` (1 or 0), `&&`, `||`, `!`, and `a ? b : c`. |

The presets show how it works. Mound is `h * smooth(1 - d / r)`, Cone `h * max(0, 1 - d / r)`, Bowl
`-h * smooth(1 - (d / r) ^ 2)`, and Moat `-h * bell((d - 0.75 * r) / (0.12 * r))` (a ditch at three quarters of the
radius). Two more examples: `h * smooth(1 - d / r) * (1 + 0.3 * n(x / 8, z / 8))` is a bumpy hill, and
`abs(x) < 3 ? -h : 0` digs a 6 m wide trench across the circle.

The Mask and the ±8 m limit apply (No limit lifts it). Points where the formula gives no number (a division by zero,
for example) are left as they were. If it gives 0 everywhere, the status bar says nothing changed.

## Mountain

The **Mountain** tool, in the rail's **CREATE** section, raises a whole mountain with one click, past the ±8 m.
Before you click, the mountain shows under the pointer as an orange mesh, as high as it will rise, and the ring shows
how far it reaches. The ring turns red where the mountain does not fit in the open area, and a click there does
nothing. Each mountain is made from a seed, so no two are alike.

| Lone peak | Ridge |
|---|---|
| ![A lone peak](images/mountain-peak.jpg) | ![A ridge](images/mountain-ridge.jpg) |
| **Volcano** | **Rolling hills** |
| ![A volcano with its crater](images/mountain-volcano.jpg) | ![Rolling hills](images/mountain-hills.jpg) |

*Four presets on a 5 × 5 area, seen from low down. The lone peak (80 m) has the biome's trees grown on it
(**Grow the biome's trees and rocks on it**) and still shows the status message; the others are left bare so their
shape shows: a long ridge, a volcano with its crater, and a few gentle hills.*

| Control | What it does |
|---|---|
| **Preset** | The kind of mountain (below). Choosing one rolls a new mountain of it, like **Randomize**. |
| **Height** | How high it rises above the ground there, 5 to 300 m. |
| **Radius** | How far it reaches from the click, 20 to 280 m. Its outline is ragged, up to 15% further. |
| **Rough** | 0: smooth slopes. 1: deep gullies and ridges. |
| **Turn** | Ridge and Mountain range only: the direction it runs, 0° to 179°. |
| **Seed** | The number its detail comes from: the same preset, values and seed make the same mountain again. |
| **Randomize** | A new mountain of the preset: new size, roughness, direction and seed. If it would not fit in the open area, it is made smaller as a whole (its height too, so the slopes stay as gentle). |
| **Take away the trees and rocks it buries** | On by default. Trees, rocks, ore, bushes and pickables where it raises the ground more than 2 m are taken away (saving would do it anyway). Building pieces stay. |
| **Grow the biome's trees and rocks on it** | On by default. Then grows the biome's own vegetation by the game's rules (as the Area tool's [Regrow](area.md) does) on the slopes it raised by more than a metre, away from what already stands there. It is a second undo step. |

| Preset | Height / radius it rolls | Shape |
|---|---|---|
| **Lone peak** | 50–110 m / 120–240 m | One mountain rising to a peak, with ridges down its sides. |
| **Ridge** | 35–80 m / 140–260 m | A long crest, half as wide as it is long. |
| **Mountain range** | 40–90 m / 180–280 m | Three to five peaks along a line, joined by saddles. |
| **Mesa** | 20–50 m / 70–150 m | A flat top with steep, rocky sides: room to build up there. Roughness goes only on the sides. |
| **Volcano** | 45–100 m / 130–240 m | A cone with a crater at the top. |
| **Rolling hills** | 12–30 m / 100–220 m | Three to six gentle hills together. |

**How a mountain is made.** Its body is a natural profile, steeper up high and easing out at the foot. Its outline is
warped by noise, so it is never a perfect circle. **Rough** then adds broad swells and ridged noise down the slopes
(ridges with gullies between them), in proportion to the mountain's size, and a little fine detail capped at a few
metres (deep, narrow cuts would look torn, and the ground discs cannot follow them). At its edge it fades back into
the ground. The mountain is added to the ground that is there, so it keeps the land's own lie.

**Good to know.**

- It must fit in the open area, with its ragged edge. Open a bigger area (the area size list in the top bar) for big
  mountains: on a 5 × 5 area the radius can be up to about 137 m.
- The Mask applies, all or nothing: where the Mask leaves ground out, the mountain stops in a cliff.
- Gentle slopes keep grass and trees; steep ones show bare rock, as the game draws them.
- One click is one undo step (two with what grew). Saving turns the mountain into ground discs, as with
  [No limit](#no-limit).
- A [script](scripting.md) can place mountains too, by rule.
