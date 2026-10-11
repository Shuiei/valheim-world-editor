# Piece facts (verified in the editor)

Everything here was checked with a mock-up or a screenshot. `piece_info` bounds of 1×1×1 with origin at
the bottom are usually placeholders: don't trust them for marble, grausten or furniture.

Yaw: 0 = the piece's front faces north (+z). Facing angle φ of a wall face: 0 north, 90 east, 180 south,
270 west; outward normal n = (sin φ, cos φ); along-wall tangent t = (cos φ, −sin φ).

## Walls, floors, stone

| Piece | Size | Origin / notes |
|---|---|---|
| stone_wall_2x1 | 2×1×1 | centre. Rows: y = ground + 0.5 + r |
| stone_wall_1x1 | 1×1×1 | centre |
| stone_wall_4x2 | 4×2×1 | centre |
| stone_floor_2x2 | 2×1×2 | centre (top = y + 0.5). Interior floor on ground: centre at ground − 0.44 |
| wood_floor | 2×0.13×2 | origin ≈ top surface |
| stone_arch | 2×1×1 | pair over a 4 m opening: lower-coordinate piece yaw 180, higher yaw 0 (for walls along x; along z: lower yaw 90, higher 270). A 6 m opening can't take a true arch |
| Piece_grausten_wall_arch | 1 m quarter curve | pair over a 2 m window, on its top row: piece at centre − 0.5·t yaw φ+180, at centre + 0.5·t yaw φ |
| Piece_grausten_window_2x2 | 2×2×0.4 | origin bottom-centre. Reads as a grey panel with a slit: avoid |
| crystal_wall_1x1 | 1×1×0.3 | glass pane, origin bottom-centre. Alone it reads as an empty hole |
| iron_wall_1x1 | 1×1×0.1 | cage lattice, origin bottom. In front of crystal = leaded glass (the approved window) |
| iron_grate | 2×3×0.2 | origin 1 m above its bottom. Barred windows; a half-raised portcullis |
| darkwood_decowall | 1×2×0.1 | carved screen, origin bottom: a showpiece window (oriel) |
| stone_fence | 2×1×1 | looks like a rough cobble pile: avoid for balustrades |
| blackmarble_2x1x1 | 2×1×1 | clean dark rail / balustrade |
| blackmarble_tip | quarter spire | 2×2 spire: yaw 0 at (+0.5, −0.5), 90 at (−0.5, −0.5), 180 at (−0.5, +0.5), 270 at (+0.5, +0.5) from the centre, base at y |
| blackmarble_out_1 | cornice wedge | flush with the wall: reads as a flat dark band, not a projection |
| blackmarble_column_1 / _2 | 1 m drums, **centred** | _2 is 2 m wide (chunky). Stack centres at ground+0.5…; one extra drum into an arch's foot; blackmarble_base_1 as plinth |
| wood_window (shutter) | 1.15×1.05 | stacked pairs look like doors: the user rejected them |

## Roofs

| Piece | Notes |
|---|---|
| piece_grausten_roof_45 | **red terracotta tiles**, 2×2 m, rise 2 per 2 run. Low edge toward local +z: yaw 0 sheds north, 180 south, 90 east, 270 west. For a 12 m deep wing: rows at offsets 1, 3, 5 from each wall line, y = eave + offset |
| darkwood_roof_67 | steep shingles. Origin 1 m in from and 1 m above the low edge; rise 4 per 2 run. Cone spire: rings each 2 m in and 4 m up, yaw = outward angle, count ≈ 2π·r/1.9 |
| darkwood_roof (26°) | origin at the low edge; two slopes meeting at a ridge need **no** darkwood_roof_top (it floats; it bridges a 2 m gap between slopes) |
| wood_roof*, darkwood_roof_*corner* | standard Valheim roof kit |

## Timber

| Piece | Notes |
|---|---|
| darkwood_pole / darkwood_pole4 | 2 m / 4 m, **centred** (y = bottom + 1 / + 2). Carved look |
| darkwood_beam / darkwood_beam4x4 | 2 m / 4 m along local x, centred. Yaw 90 runs along z |
| darkwood_beam_45 | knee brace. In a hall along x with posts on the south and north walls: south braces yaw 0, north braces yaw 180, centre 1.2 m in from the wall, 1.15 m below the beam |
| wood_stair | 2×1.12×2: rises 1 m per 2 m, low edge toward local +z, origin at its low base |

## Doors, gates, lights

| Piece | Notes |
|---|---|
| wood_door | 2×2, centred (y = floor + 1) |
| darkwood_gate | 2×4, origin bottom. Double gate: left leaf yaw = facing, right leaf yaw = facing + 180 (hinges at the edges, handles meet). Put it in the doorway, not deep in a passage |
| piece_walltorch | sconce: yaw = the direction it points into the room; 0.35 m off the wall face |
| piece_dvergr_lantern_pole | origin at its **top**: y = ground + 3 |
| piece_brazierceiling01 | hangs ~1.5 m below its chain top: y ≈ ceiling − 1.5 |
| piece_brazierfloor01/02, braziers, fires | drawn as blue wireframes in the editor (no model): fine in game |

## Furniture (facing found by testing)

| Piece | Facing |
|---|---|
| piece_blackmarble_throne | yaw 270 faces west |
| piece_chair03 | yaw 0 faces east, 180 west (not what the yaw convention suggests: check every new chair) |
| piece_bed02 (dragon bed) | yaw 180 puts the carved headboard at +z; bounds z −1.6…+1.84 |
| piece_oven | front is local +x: yaw 90 opens south |
| piece_banner* | cloth lies along local z: on a wall facing ±x use yaw 0, on ±z use 90; 0.4 m off the wall or it sinks in |
| piece_table_oak | 6.45 m along x; benches piece_bench01 2.4 m |
| hearth | 4×3 stone hearth, origin bottom; piece_cookingstation_iron over it at +0.4; piece_cauldron on its rim at +0.58 |
| piece_table_round | top at its origin: y = floor + 0.8 |
| ArmorStand, itemstand, itemstandh | empty unless filled with set_contents (stand support); ArmorStand shows as a white mannequin in the editor |

## Food and stands

- The serving tray is the **Feaster** tool: its 119 pieces are the food items themselves (CookedMeat,
  HoneyGlazedChicken, Bread, MeadTasty, FeastMeadows…). Placed with place_pieces they need the int
  `piece` = 1 to stay put in game (the editor writes it on new Feaster pieces).
- itemstand (wall) takes weapons, shields, helmets, tools, trophies, fish, ammo, trinkets; itemstandh
  (flat) also food and potions. Raw materials without an attach point fit nothing.
- ArmorStand slots: chest, legs, helmet, shield, weapon/tool, cape, utility (each twice).

## Landscape objects

Trees: Oak1, Beech1, Beech_small1, Birch1, FirTree. Bushes: Bush01. Rocks: rock4_forest (find_prefabs).
Paint kinds: dirt, cultivated, paved, clear.
