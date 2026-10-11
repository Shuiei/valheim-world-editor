# Piece notes: what the tools don't tell you

This is not a catalogue. The editor has every piece of the game: `find_prefabs` searches them (name,
build tool — hammer, hoe, cultivator, feaster — size, cost; `items: true` for items), and `piece_info`
gives sizes, bounds and snap points. Use those to find and measure pieces.

What they can't tell you is collected here: which way a model really faces, where its origin is when
`piece_info`'s bounds are placeholders (1×1×1 with the origin at the bottom: common for marble, grausten
and furniture), how a piece looks once placed, and the traps. Everything below was checked with a mock-up
or a screenshot. Add to it whenever a mock-up or the user teaches something new.

Yaw: 0 = the piece's front faces north (+z). Facing angle φ of a wall face: 0 north, 90 east, 180 south,
270 west; outward normal n = (sin φ, cos φ); along-wall tangent t = (cos φ, −sin φ).

## Origins and stacking

- stone_wall_* and stone_floor_2x2 are centred: wall rows at y = ground + 0.5 + r; a floor's top is its
  y + 0.5. An interior floor on the ground: centre at ground − 0.44 (at −0.5 the grass flickers through),
  and paint the ground "paved" under it.
- darkwood_pole / darkwood_pole4 and blackmarble_column_1/2 are centred (1 m drums for the columns): a
  column under an arch needs one more drum tucked into the arch's foot.
- wood_floor's origin is about its top surface. wood_door is centred (y = floor + 1); darkwood_gate's and
  crystal_wall_1x1's origin is at their bottom; iron_grate's is 1 m above its bottom.
- piece_dvergr_lantern_pole: origin at its **top** (y = ground + 3). piece_brazierceiling01 hangs about
  1.5 m below its chain's top. piece_table_round: its top is at its origin (y = floor + 0.8).
- darkwood_roof_67: origin 1 m in from and 1 m above its low edge; rises 4 m per 2 m.
- wood_stair: rises 1 m per 2 m, low edge toward local +z, origin at its low base.

## Which way things face (found by testing)

- piece_blackmarble_throne: yaw 270 faces west.
- piece_chair03: yaw 0 faces east, 180 west — not what the convention suggests; check every chair kind.
- piece_bed02 (dragon bed): yaw 180 puts the carved headboard at +z.
- piece_oven: its front is local +x (yaw 90 opens south).
- piece_banner*: the cloth lies along local z — on a wall facing ±x use yaw 0, on a wall facing ±z use 90 —
  and hangs 0.4 m off the wall or sinks into it.
- piece_walltorch (sconce): yaw = the direction it points; 0.35 m off the wall face.
- Roof slopes (wood_roof*, darkwood_roof*, piece_grausten_roof_45): the low edge is toward local +z
  (yaw 0 sheds north, 180 south, 90 east, 270 west).
- darkwood_beam_45 knee braces in a hall along x: on the south wall yaw 0, on the north wall yaw 180,
  centred 1.2 m in from the wall and 1.15 m under the beam.
- stone_arch pair over a 4 m opening: the lower-coordinate piece yaw 180, the higher yaw 0 (along z:
  lower 90, higher 270). A 6 m opening can't take a true arch.
- Piece_grausten_wall_arch pair over a 2 m window, on its top row: at centre − 0.5·t yaw φ + 180, at
  centre + 0.5·t yaw φ.
- blackmarble_tip quarter spires make a 2×2 spire: yaw 0 at (+0.5, −0.5), 90 at (−0.5, −0.5), 180 at
  (−0.5, +0.5), 270 at (+0.5, +0.5) from its centre.
- darkwood_gate double gate: left leaf yaw = facing, right leaf yaw = facing + 180 (hinges at the edges,
  handles meeting); in the doorway, not deep in a passage.

## How pieces look (the user's and the screenshots' verdicts)

- piece_grausten_roof_45 is **red terracotta**: the castle's approved roofs.
- Leaded glass = crystal_wall_1x1 panes 0.2 m in, iron_wall_1x1 lattice in front: the approved window.
  Clear crystal alone reads as an empty hole; Piece_grausten_window_2x2 as a grey panel with a slit;
  stacked wood_window shutters as doors (rejected).
- iron_grate makes good barred windows and a half-raised portcullis; darkwood_decowall a carved showpiece
  window.
- stone_fence looks like a rough cobble pile: use blackmarble_2x1x1 or stone_wall_2x1 for rails.
- blackmarble_out_1 cornices sit flush: a flat dark band, not a projection.
- blackmarble_column_2 is 2 m wide (chunky); column_1 is the slim one.
- darkwood_pole/pole4 are carved: the posts that make timber framing look good.
- Fire props (braziers, hearth fire) show as blue wireframes in the editor; they are fine in game.
- ArmorStand shows as a white mannequin in the editor.

## Traps

- Two 26° slopes meeting at a ridge need no darkwood_roof_top: it floats above them (it bridges a 2 m gap).
- Cone spires: the tip is the highest ring's origin + 3; a flagpole placed from a guessed height floats.
- Building pieces don't keep a scale in game: place them at scale 0.
- Every part of a window takes the window's yaw φ, never 0/90: on round towers a snapped sill sticks out.
- The serving tray is the **Feaster** build tool: its pieces are the food items themselves (CookedMeat,
  MeadTasty, FeastMeadows…). They need the int `piece` = 1 to stay put in game (editors with Feaster
  support write it).
- Item stands: itemstand (wall) takes weapons, shields, helmets, tools, trophies, fish; itemstandh (flat)
  also food and potions; items without an attach point (raw materials) fit none. Fill them, and armour
  stands, with `set_contents` where the editor supports stands.
