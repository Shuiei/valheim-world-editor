# Ground limits

The game lets ground move at most 8 m up or down from where the world generated it. The editor keeps to that rule, so
every world it saves loads in every player's game, console players included, with no mod. **No limit** goes past it
in a way the game itself understands. This page explains the limit, how No limit gets round it, and how the game's
locations shape the ground.

## The game's 8 m limit

The game does not store the ground's height. It stores the world's **seed**, works out the generated ground from it,
and saves only your **changes** to it: for each ground point, how far it was raised or dug. That change can be at most
**±8 m** (plus a smoothing change of at most ±1 m, which the game's hoe makes). Anything further is thrown away when
the game loads the zone. The editor applies the same rule, so a point that reaches the limit stops there.

What this means in practice:

- **It is measured from the generated ground, not from where the ground is now.** A hill raised 8 m cannot go higher,
  and ground dug 8 m cannot go deeper, however you got there. The status bar shows both heights under the mouse:
  `ground 41.20 m (original 35.00, +6.20)`.
- **Big works hit it first**: a building pad cut into a hillside, a river through high ground (it needs its bed below
  the sea, see [How the ground is stored](#how-the-ground-is-stored)), a mountain.
- **The game's own tools keep to it too.** A player's hoe and pickaxe work within ±8 m of the generated ground, in game
  as in the editor.

```text
        ┌──────────────────────── +8 m: the highest the game keeps
        │      ╭──╮  raised ground stops here
  ──────┼─────╯    ╰──────────── generated ground (the "original" height)
        │
        └──────────────────────── −8 m: the deepest the game keeps
```

## No limit

The **No limit: past the game's ±8 m** switch lets the ground tools move the ground up to 400 m from the generated
ground: mountains, cliffs, canyons, deep caves. It is in the brush options and under the Area tool's Flatten, Raise
and Lower (the same switch), and it is off each time the editor starts. The [Mountain](../terrain.md) tool and
pasting a copy always go past the limit when they need to, and scripts do unless told not to.

While you work, the part past ±8 m is kept as a separate lift on top of the ground; undo, History and **Restore** treat
it like any other change. The status bar says which way the switch is each time you turn it.

![The same Raise stroke twice on flat ground: on the left stopped at 8 m with a red top, on the right a spire far higher](../images/no-limit.jpg)
*The same stroke without and with No limit. Left, the ground stops 8 m up and its top turns red; right, it keeps
rising. The brush panel shows the No limit switch.*

### How it is saved: ground discs

The game never keeps ground edits past 8 m, but it does level the ground under its **locations** (villages, the
trader's camp) without any limit, and counts that levelled ground as generated (see
[Locations level the ground](#locations-level-the-ground)). The editor uses this. When you save or apply live, it
turns ground past the limit into **ground discs**: invisible locations of the game's own, with no model. Each disc
levels a circle of 8 m radius to its height and blends the ground around it out to about 13 m.

1. Discs go on a 6 m grid, only where your ground is more than 5 m away from what the ground would be without them.
2. The editor works out the ground exactly as the game will build it from the discs, in the game's order, and adjusts
   their heights over several rounds to come as close as it can to your shape. Where it is still too far off, it adds
   a disc and tries again.
3. Ordinary ground edits, within ±8 m of the new ground, make up the rest.

Every player's game builds the same ground from the discs, console players included, with no mod.

<!-- picture: concept-ground-discs.jpg: a side diagram (cross-section) of a hill raised 30 m with No limit: the
generated ground line, a row of flat disc tops 6 m apart under the new surface, each disc's 8 m flat circle and its
blend out to 13 m, and the thin ±8 m band of ordinary edits on top that makes up the final shape -->

### What it means for you

| | |
|---|---|
| **The save message** | Says how many discs were made (and how many old ones they replace), how many trees and rocks were taken away, and how far off the steepest ground still is: "No limit ground: 312 ground disc(s), 40 tree(s)/rock(s) taken away; some ground is up to 2.5 m off (too steep)". |
| **History starts over** | The editor reads the world again from the new save, because the ground under the area is no longer the same. Check the shape before you save. |
| **The new ground counts as generated** | After saving, the status bar's "original" height is the disc ground. The hoe, the pickaxe and the editor's tools work ±8 m from it, and **Restore** brings ground back to it, not to the world's first ground. To take a saved shape away, work it down again with No limit on: the discs are worked out again at the next save. |
| **Trees and rocks are taken away** | Trees, rocks, ore, bushes and pickables where the ground moved more than 2 m would be buried or float, so they go. Building pieces stay. |
| **Very steep walls come out softer** | A disc cannot follow a cut narrower than itself. |
| **Zones nobody has visited** | Keep their old ground: their lift is dropped, and the message says how many. Visit them in game first. |
| **The discs load first** | A player's game makes the discs before the pieces and trees around them, so the ground is finished in the first moments of a visit. On a server, the WorldEditorBridge plugin keeps it that way. |
| **Seen from afar** | The new shape appears as you come near, like a village's levelled ground. |
| **The in-game map** | Is drawn from the seed, so it does not show the new shape. |
| **Dirt** | The game paints a disc's ground as dirt; the editor paints those points back with the biome's own ground, so the discs do not show. |

The editor hides the discs: they show as no location, marker or object.

## Locations level the ground

Villages, the trader, dungeon entrances, bosses' altars and the game's other **locations** flatten the ground around
them. The flat ground is not stored in the save: the game levels it again every time it loads the zone, before it
applies anyone's changes. Then it adds the saved ground edits on top.

The editor shows the ground with that levelling already done, and your edits go on top of it, as in game. A note at
the bottom left of the 3D view counts the locations in the area (and within 40 m of it), and **View → Location
flattening** draws where they level: a ring for the flat part, a fainter one where it blends into the land.

<!-- picture: concept-location-flattening.jpg: a 3 × 3 area round a Meadows village (or the trader's camp) in Game
look, seen from above at an angle: View panel open with Location flattening on and its rings drawn round the
location (the solid ring of the flat part, the fainter ring where it blends), the note at the bottom left counting the
locations, nothing else selected -->

Why it matters:

- **The ±8 m is counted from the levelled ground**, since the game counts it as generated.
- **Removing a location removes its levelling.** Deleting a runestone takes away its location for good, and the
  small levelling round it with it. A zone reset places the location again, and its levelling comes back.
- **Parts of a location are not objects.** A crypt entrance's rocks, the trader's camp, a boss's altar or a troll
  cave's walls are made again from the location each time the zone loads, so the editor draws them (under **Ruins &
  structures**) but cannot pick or change them.
- **Some locations keep their ground clear.** Nothing grows in the square round a location whose kind clears its
  ground, and Regrow keeps it so, as the game does (see [Growing rules](objects.md#growing-rules)).
- **Ground discs** are this same mechanism, used on purpose (see [No limit](#no-limit)).

## Limit marks

Ground that reached the ±8 m limit turns **red** in the 3D view (in Game look; **View → Limit marks**, on at first),
and the status bar adds ", at the ±8 m limit" under the mouse. After a stroke that hit it, it says "Reached the game
limit: ground can only move 8 m from its original height (red points)."

The other tools say it their way:

| Tool | How it shows the limit |
|---|---|
| [Path](../path.md) | The points of the line turn red, and the status bar says so. |
| [Area](../area.md): Flatten, Raise, Lower | The cut-and-fill line says how much is out of reach: "(40 m³ out of reach: ±8 m limit)". |
| Area: Heightmap | Points that cannot reach their height turn red, and the status bar counts them. |
| [Script](../scripting.md) | With `Ground.NoLimit = false`, the result line counts the points that stopped. |

Red ground is not an error: it is ground the game would not let go further. To go on, turn on **No limit**, or change
the plan (a river that follows low ground, a pad cut a little higher).

## How the ground is stored

**One height per metre.** The ground has one height point per metre: 65 × 65 points in a 64 m zone, neighbouring
zones sharing their edge points. So a wall of ground always spans about a metre: it cannot be more upright than that,
in the editor or in game, and the ground can never overhang. That is why a cave is dug ground roofed with rocks, like
the game's own (see the [Path](../path.md) tool's Cave).

**Two triangles per square metre.** Each square metre is drawn as two triangles split along the same diagonal as in
game, from its south-east to its north-west corner. An edge running north-west to south-east comes out clean; one
running north-east to south-west shows a small sawtooth, in game too.

**Heights are the game's.** The sea is at 30 m. Valheim has no flowing water: the water in rivers and lakes is the sea,
at the same level everywhere, so ground has water on it only where it is below 30 m. A river through high ground needs
a deep cut.

**Paint.** Each ground point also has a paint value: a share of dirt, of cultivated soil and of paved stone, and a mark
saying it was painted. A point never painted shows the paint the world generated (the biome's ground, and dirt where
the world put it). A paint brush moves the point's shares a little towards its colour each moment, so a light stroke
or the brush's soft edge gives a blend. That is also why **Clear paint** and **Restore** differ:

| | What it does to a point |
|---|---|
| **Clear paint** | Paints plain biome ground. The point stays painted, so it also hides dirt the world generated there. |
| **Restore** | Removes the paint mark, so the point shows what the world generated again. |

**Biome.** The biome at a point comes from the seed. Editing the ground never changes it.

## How the Mask judges the ground

The [Mask](../masks.md) limits a tool to the ground that matches all of its settings. It judges each ground point on
its own, and there is no blend: where it leaves ground out, the tool does nothing at all; where it lets ground
through, the tool works as usual.

| Setting | What is compared |
|---|---|
| **Biome** | The biome the world generated at that point. Editing the ground does not change it. |
| **Height** | The ground's height there, as the game counts it (the sea is at 30 m). |
| **Slope** | Worked out from the heights 1 m either side of the point, east–west and north–south. |
| **Paint** | A point counts as painted when it was painted and has some colour, so **Clear paint** leaves it unpainted for the Mask. **Only dirt**, **Only cultivated** and **Only paved** need that colour to be at least half of the paint. |

**A brush stroke is judged on the ground as it was when the stroke started.** So a Raise stroke on ground below the
Mask's height keeps rising past it until you let go, and a stroke that changes the slope does not switch itself off
halfway. Edits made all at once (Mountain, Shape, a paste, Stamp once, Path, the Area tool's actions) are each judged on
the ground just before them.

Because there is no soft edge, sculpting with a Mask can leave a step where matching ground meets ground left out,
most of all with Raise, Lower and Mountain. Smooth it afterwards with the Mask off.

## How the tools shape the ground

Most tools simply raise, dig or level. A few follow a pattern or a model worth knowing, because it explains what they
give and what they cannot.

### How fast a brush works

At **Strength** 1, Raise and Lower move the middle of the brush by up to 6 m per second (2.4 m per second at the
default 0.40). Flatten, Smooth, Naturalize and Restore add no fixed amount: each moment they move every point a share
of the way towards their aim, so the ground settles on the aim and never overshoots it. Smooth moves each point
towards the average of itself and its 8 neighbours, so going over a place again smooths it more. Restore removes a
share of the change each moment, and a point within 1 cm of its generated height counts as unchanged again; it
removes paint only where the brush works at more than half strength, so the paint's edge is not frayed.

### The Naturalize pattern

**Naturalize** first works out the ground's broad shape: the ground blurred over a few metres, as it was when the
stroke started. It adds bumps from a noise pattern to that shape, **Bumps** high and **Bump size** wide, and pulls the
ground towards the result. Because the aim stays the same, a second stroke over the same place does not pile up more
and more bumps: it only comes closer to the same ground.

The pattern is fixed to the world, not to the brush, so neighbouring strokes and areas join without seams. The
Path tool's natural look, the Area tool's Naturalize and the Shape tool's `n(x, z)` use the same pattern; **New
pattern** changes it for all of them.

### Erosion

**Thermal** erosion moves ground wherever two neighbouring points are steeper than the **Rest angle**: material slides
from the higher point to its lower neighbours, half of the extra at a time, so slopes settle instead of swinging.
Ground is only moved, never lost, which makes screes below cliffs. **Water** erosion is rain: drops fall on random
points and run downhill for up to 40 steps, picking up ground where they run fast and dropping it where they slow down
or fill a pit. Many drops cut gullies down slopes and leave fans of material at their feet. Ground just outside the
brush may receive what slides or washes out of it.

### How a mountain is made

A mountain's body is a natural profile, steeper up high and easing out at the foot, and its outline is warped by
noise so it is never a perfect circle (up to 15 % further than the radius). **Rough** adds broad swells and ridges with
gullies between them down the slopes, in proportion to the mountain's size, and a little fine detail capped at a few
metres: deep, narrow cuts would look torn, and ground discs could not follow them. At its edge it fades back into the
land, and it is added to the ground that is there, so it keeps the land's own lie. Each mountain comes from a **seed**:
the same preset, values and seed make the same mountain again. Gentle slopes keep grass and trees; steep ones show
bare rock, as the game draws them. A mountain always goes past ±8 m, and saving turns it into
[ground discs](#how-it-is-saved-ground-discs).

### How a path's edge fades

Every ground point near a path is measured from the nearest point of its line. Inside half the **Width** the action
works fully; across the **Soft edge** it fades out along an S-curve, so a road blends into the slope above and below
it. At a soft edge of 0, the points the edge crosses get a share of the change by how far the line passes from them,
so the edge follows the line straight instead of stepping from point to point. With **Natural look**, the width varies
by up to 45 % either way and the edges are ragged, from the Naturalize pattern. A river's bed is a U shape below sea
level that only digs, so it can run into a lake or the sea.

### How a cave is built

The ground cannot overhang, so a cave is dug ground roofed with rock, like the game's own. The Path tool's **Cave**
builds it in four steps, as one undo step:

1. **The floor** follows the lie of the land: at each point of the line it is the ground's average height over 6 m
   either way along the line, minus **Depth**. Near each end the depth eases to nothing over 2.5 times the depth (at
   least 8 m), which makes the entrances. Without No limit the floor stays within the game's 8 m.
2. **The roof.** Boulders go only where the cave is deeper than **Headroom** by 1.5 m, so the entrances stay open:
   one every 0.9 × **Width** metres, then more wherever the floor still sees the sky. Each is turned upside down, so
   its flat side makes an even ceiling, then turned, tilted a little and sized at random, and set just high enough
   that its underside clears the headroom.
3. **The walls** are raised half a metre into the rock wherever a boulder's underside is above them, so no daylight
   comes in at the sides.
4. **Clearing.** Trees, rocks, ore, bushes and pickables in the trench (where it is more than half a metre deep) are
   taken away.

**By biome**, the roof uses heath boulders in the Plains, mountain rocks in the Mountains and the Deep North, coast
boulders in the Ocean and forest boulders elsewhere. From above the roof looks like a rocky outcrop. Players can mine
the boulders like any others, so a cave is not a safe vault. A closed loop makes a ring-shaped cave with one entrance
where the ends meet.
