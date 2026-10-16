using System.Numerics;
using static TerrainEditor.Editing.DungeonKit;

namespace TerrainEditor.Editing;

/// <summary>
/// The dresser's larger set pieces, from what dungeons in other games and in old crypts are made of: a gallery over
/// the way in (Skyrim's balconies, a church's loft), ledges round a sunken floor (an arena pit), a vault within the
/// room (a chamber grave, Diablo's sub-rooms), clusters of things that belong together (a camp, a meal, a scribe's
/// desk: set dressing in readable groups), one hero piece a room would be remembered by, and corridors of several
/// kinds (a gate midway, a collapse, bones along the walls, a runner).
/// </summary>
internal sealed partial class DungeonDresser
{
    /// <summary>The hero pieces already standing in this dungeon: each stands at most once.</summary>
    private readonly HashSet<string> _heroes = [];

    /// <summary>Whether the material can carry a stone deck 4 m up on posts of its own: stone and grausten do; wood and the rest do not.</summary>
    private bool Decked => _mat.Name is "Stone" or "Grausten";

    /// <summary>The deck piece (2 × 2 m, 1 m thick) of the material: stone slabs for both.</summary>
    private const string Deck = "stone_floor_2x2";

    /// <summary>A column where the floor is free (not in a doorway's way, nor on the loft's stairs); nothing otherwise.</summary>
    private void Pillar(Frame f, float u, float v)
    {
        if (f.Free(u, v, 0.8f))
        {
            Column(f, u, v);
        }
    }

    /// <summary>The kinds of cluster that suit the style's halls, crossings and entrance.</summary>
    private string[] StyleClusters => _style.Name switch
    {
        "Crypt" or "Catacombs" => ["bones", "altar"],
        "Temple" => ["altar", "scribe"],
        "Fortress" or "Prison" => ["armoury", "meal"],
        "Dvergr hold" => ["stores", "workshop"],
        "Goblin warren" => ["camp", "bones"],
        _ => ["bones", "stores"],
    };

    /// <summary>A post of the material from the floor to h metres up at (u, v), keeping the floor round it.</summary>
    private void Post(Frame f, float u, float v, float h)
    {
        var p = f.W(u, v, 0);
        DungeonShell.Stack(_mat.Column[0], _o, p.X, p.Z, f.F, f.F + h);
        f.Take(u, v, 0.8f);
    }

    // ---- The gallery.
    /// <summary>
    /// A loft over the way in of a tall stone room: a deck 2 m deep along the near wall, 4 m up on posts, a rail
    /// along its edge, stairs up one side (8 m of them: a stair rises 1 m over 2), braziers and a sentinel on it.
    /// None when the room is low, not of stone, too small for the stairs, has a tall doorway (a grand gate, a
    /// curtain) in that wall or its sides are not free along the stairs.
    /// </summary>
    /// <returns>Whether the loft stands (its stairs then keep their side: colonnades must check the floor is free).</returns>
    private bool Loft(Frame f)
    {
        if (!f.S.Tall || !Decked || f.HalfA < 3 || f.HalfD < 6 || f.S.Shape != DungeonPlan.Shape.Rect)
        {
            return false;
        }
        foreach (var m in f.Mouths)
        {
            var (_, mv) = f.Local(m.X, m.Z);
            if (mv < -f.HalfD + 1.5f && m.Door.Kind is Door.Grand or Door.Curtain)
            {
                return false;
            }
        }
        // The deck's row is the room's first; its edge 2 m in from the near wall's tiles.
        float edge = -f.Deep + 2;
        int side = 0;
        foreach (int s in new[] { -1, 1 }.OrderBy(_ => _rnd.Next()))
        {
            float tryU = s * (f.HalfA - 1.05f);
            if (Enumerable.Range(0, 4).All(i => f.Free(tryU, edge + 1 + 2 * i, 1.0f)))
            {
                side = s;
                break;
            }
        }
        if (side == 0)
        {
            return false;
        }
        // The deck, and the posts under its edge (none in front of a doorway).
        for (int a = 0; a < f.Across; a++)
        {
            float u = 2 * a + 1 - f.Across;
            _o.Add(Deck, f.W(u, edge - 1, f.F + 3.5f), f.Yaw(0));
            if (f.Free(u, edge - 0.5f, 0.6f))
            {
                Post(f, u, edge - 0.5f, 3);
            }
            if (a != (side < 0 ? 0 : f.Across - 1))
            {
                _o.Add("stone_fence", f.W(u, edge - 0.5f, f.F + 4 + Lift), f.Yaw(0));
            }
        }
        // The stairs: a stepped base of slabs along the side wall, a stair on each step, one on the floor.
        float su = side * (f.HalfA - 1.05f);
        for (int k = 0; k < 4; k++)
        {
            float v = edge + 1 + 2 * k;
            for (int b = 0; b < 3 - k; b++)
            {
                _o.Add(Deck, f.W(su, v, f.F + 0.5f + b), f.Yaw(0));
            }
            _o.Add("stone_stair", f.W(su, v, f.F + (3 - k) + Lift), f.Yaw(0));
            f.Take(su, v, 1.2f);
        }
        // Up on the deck: braziers at both ends, a sentinel in the middle facing the room, skulls.
        float top = f.F + 4 + Lift;
        float deckV = edge - 1;
        _o.Add(_b.Brazier, f.W(-(f.HalfA - 1.1f), deckV, top + DungeonProps.Foot(_b.Brazier)), f.Yaw(0));
        _o.Add(_b.Brazier, f.W(f.HalfA - 1.1f, deckV, top + DungeonProps.Foot(_b.Brazier)), f.Yaw(0));
        string sentinel = _style.Name switch
        {
            "Crypt" or "Catacombs" or "Ruins" => One("StatueEvil", "StatueDeer", "skull_pile"),
            "Temple" => One("StatueFreya", "StatueThor"),
            "Goblin warren" => "goblin_totempole",
            "Dvergr hold" => "dvergrprops_lantern_standing",
            _ => One("ArmorStand_Male", "Morkhalla_WeaponStand"),
        };
        _o.Add(sentinel, f.W(0, deckV - 0.2f, top + DungeonProps.Foot(sentinel)), f.Yaw(0));
        return true;
    }

    // ---- Ledges round a pit.
    /// <summary>
    /// Ledges 1 m high round the room's edge, leaving the middle a sunken floor and the doorways' tiles level with
    /// it: an arena's stands, a sunken shrine. The ledges keep their floor; braziers stand on their corners.
    /// </summary>
    private void Ledges(Frame f)
    {
        var ledges = new List<(float U, float V)>();
        for (int a = f.A0; a <= f.A1; a++)
        {
            for (int b = f.B0; b <= f.B1; b++)
            {
                if (!f.Has(a, b) || f.Has(a - 1, b) && f.Has(a + 1, b) && f.Has(a, b - 1) && f.Has(a, b + 1))
                {
                    continue;
                }
                float u = 2 * a + 1 - f.Across, v = 2 * b + 1 - f.Deep;
                if (f.NearDoor(u, v, 2.6f))
                {
                    continue;
                }
                _o.Add(Deck, f.W(u, v, f.F + 0.5f + Lift), f.Yaw(0));
                f.Take(u, v, 1.3f);
                ledges.Add((u, v));
            }
        }
        // Braziers on the ledges' corners (the tiles with two free sides), skulls on some others.
        foreach (var (u, v) in ledges)
        {
            bool corner = MathF.Abs(MathF.Abs(u) - (f.Across - 1)) < 0.1f && MathF.Abs(MathF.Abs(v) - (f.Deep - 1)) < 0.1f;
            if (corner)
            {
                _o.Add(_b.Brazier, f.W(u, v, f.F + 1 + Lift * 2 + DungeonProps.Foot(_b.Brazier)), f.Yaw(0));
            }
            else if (Chance(0.25))
            {
                _o.Add(One("skull_pile", "bone_stack", "Skull1"), f.W(u, v, f.F + 1 + Lift * 2), f.Yaw(R(0, 360)));
            }
        }
    }

    // ---- A vault within the room.
    /// <summary>
    /// An inner chamber against the far wall: walls of the material 3 m high, w metres across and d deep, with a 2 m
    /// opening in the middle of its front under a lintel; candles either side of the opening. What it holds goes in
    /// after (its middle is (0, HalfD - d / 2)). The floor along its walls is kept.
    /// </summary>
    private void Vault(Frame f, float w, float d)
    {
        float front = f.HalfD - d, half = w / 2;
        InnerWall(f, -half, front, -half, f.HalfD, 0, 3);
        InnerWall(f, half, front, half, f.HalfD, 0, 3);
        InnerWall(f, -half, front, -1, front, 0, 3);
        InnerWall(f, 1, front, half, front, 0, 3);
        InnerWall(f, -1, front, 1, front, 2, 3);
        for (float t = 0; t <= d; t += 1)
        {
            f.Take(-half, front + t, 0.5f);
            f.Take(half, front + t, 0.5f);
        }
        for (float t = -half; t <= half; t += 1)
        {
            if (MathF.Abs(t) > 1.2f)
            {
                f.Take(t, front, 0.5f);
            }
        }
        if (!_s.Respawn)
        {
            _o.Add("Candle_resin", f.W(-1.3f, front - 0.4f, f.Top + Lift), f.Yaw(R(0, 360)));
            _o.Add("Candle_resin", f.W(1.3f, front - 0.4f, f.Top + Lift), f.Yaw(R(0, 360)));
        }
        else
        {
            Put(f, "skull_pile", -1.4f, front - 0.6f, R(0, 360), 0.3f);
            Put(f, "skull_pile", 1.4f, front - 0.6f, R(0, 360), 0.3f);
        }
    }

    // ---- Clusters.
    /// <summary>The style's dining table, bench and seat.</summary>
    private (string Table, string Seat, string Bench) Furniture => _mat.Name switch
    {
        "Dvergr" => ("dvergrprops_table", "dvergrprops_stool", "dvergrprops_table"),
        "Grausten" => ("Morkhalla_Table", "Morkhalla_Stool", "Morkhalla_Bench"),
        "Marble" => ("piece_table_oak", "piece_chair03", "piece_blackmarble_bench"),
        "Stave" => ("piece_table_runed", "piece_chair_runed", "piece_bench_runed"),
        _ => ("piece_table", "piece_chair", "piece_bench01"),
    };

    /// <summary>
    /// A cluster of things that belong together, anchored at (u, v) and facing yaw (into the room): a camp (bedroll,
    /// fire, firewood), a meal (table, seats, food), a scribe's desk (table, chair, candles, chest), stores (crates
    /// stacked, barrels), an armoury (weapon and armour stands), an altar (statue, candles, offerings), bones
    /// (a stack, skulls, a long bone), a workshop (bench and tools). Whatever has no room is left out.
    /// </summary>
    private void Cluster(Frame f, string kind, float u, float v, float yaw)
    {
        var at = (u, v, yaw);
        var (fu, fv) = In(at, 1.5f);
        var (lu, lv) = Beside(at, -1.1f);
        var (ru, rv) = Beside(at, 1.1f);
        var (table, seat, bench) = Furniture;
        switch (kind)
        {
            case "camp":
                Put(f, One("Morkhalla_Bedroll1", "Morkhalla_Bedroll2"), u, v, yaw + 90 + R(-10, 10), 0.8f);
                Put(f, One("Morkhalla_firepit", "fire_pit"), fu, fv, R(0, 360), 1.0f);
                Put(f, One("prop_wood_stack", "piece_pot3_cracked", "barrell"), ru, rv, R(0, 360), 0.5f);
                break;
            case "meal":
                if (Put(f, table, u, v, yaw + 90, 1.1f))
                {
                    Put(f, Chance(0.5) ? FeastFood : "prop_Tankard", u, v, R(0, 360), 0, y: 0.82f, force: true);
                    var (bu, bv) = In(at, 1.1f);
                    var (ku, kv) = In(at, -1.0f);
                    Put(f, bench == table ? seat : bench, bu, bv, yaw + 90, 0.4f);
                    Put(f, seat, ku, kv, yaw, 0.3f);
                }
                break;
            case "scribe":
                if (Put(f, table, u, v, yaw + 90, 1.0f))
                {
                    // The table runs along the wall (yaw + 90): what stands on it keeps to its long axis. Only a table
                    // that is a building piece carries a light the support check can see; else it stands beside.
                    var (au, av) = Beside(at, 0.5f);
                    var (bu2, bv2) = Beside(at, -0.6f);
                    if (!Stability.Known(table))
                    {
                        var (su, sv) = In(at, 1.1f);
                        Put(f, _mat.Name == "Dvergr" ? "dvergrprops_lantern_standing" : _b.Torch, su, sv, yaw, 0.3f);
                    }
                    else if (!_s.Respawn)
                    {
                        Put(f, "Candle_resin", au, av, R(0, 360), 0, y: 0.82f, force: true);
                        Put(f, "Candle_resin", bu2, bv2, R(0, 360), 0, y: 0.82f, force: true);
                    }
                    else
                    {
                        Put(f, "piece_hoodedlantern", u, v, yaw, 0, y: 0.82f, force: true);
                    }
                    var (cu, cv) = In(at, 1.1f);
                    Put(f, seat, cu, cv, yaw + 180, 0.3f);
                    Put(f, _mat.Name == "Dvergr" ? "dvergrprops_crate" : "piece_chest_wood", ru, rv, yaw, 0.4f);
                }
                break;
            case "stores":
                string crate = _mat.Name == "Dvergr" ? "dvergrprops_crate" : "CargoCrate";
                string barrel = _mat.Name == "Dvergr" ? "dvergrprops_barrel" : "barrell";
                if (Put(f, crate, u, v, yaw + R(-12, 12), 0.6f) && Chance(0.6))
                {
                    _o.Add(crate, f.W(u, v, f.Top + Lift + 0.95f), f.Yaw(yaw + R(-25, 25)));
                }
                Put(f, barrel, lu, lv, R(0, 360), 0.5f);
                Put(f, Chance(0.5) ? barrel : crate, ru, rv, R(0, 360), 0.5f);
                Put(f, One("prop_wood_stack", "piece_pot1_cracked", "piece_chest_wood"), fu, fv, R(0, 360), 0.5f);
                break;
            case "armoury":
                Put(f, "Morkhalla_WeaponStand", u, v, yaw, 0.7f);
                Put(f, One("ArmorStand_Male", "ArmorStand_Female"), lu, lv, yaw + R(-8, 8), 0.5f);
                Put(f, One("ArmorStand_Male", "piece_chest_wood", "barrell"), ru, rv, yaw + R(-8, 8), 0.5f);
                break;
            case "altar":
                string idol = _style.Name switch
                {
                    "Temple" => One("StatueFreya", "StatueThor", "StatueSeed"),
                    "Goblin warren" => "goblin_totempole",
                    "Dvergr hold" => "blackmarble_altar_crystal",
                    _ => One("StatueEvil", "StatueDeer", "MountainGraveStone01"),
                };
                if (Put(f, idol, u, v, yaw, 0.7f))
                {
                    if (!_s.Respawn)
                    {
                        Put(f, "Candle_resin", lu, lv, R(0, 360), 0.2f);
                        Put(f, "Candle_resin", ru, rv, R(0, 360), 0.2f);
                    }
                    else
                    {
                        Put(f, "skull_pile", lu, lv, R(0, 360), 0.3f);
                    }
                    var (ou, ov) = In(at, 1.0f);
                    Put(f, Chance(0.5) ? _b.Pickable : "treasure_stack", ou, ov, R(0, 360), 0.3f);
                }
                break;
            case "bones":
                Put(f, "bone_stack", u, v, yaw + R(-30, 30), 0.5f);
                Put(f, "skull_pile", lu, lv, R(0, 360), 0.4f);
                Put(f, One("LargeBone", "Skull1", "skull_pile"), ru, rv, R(0, 360), 0.4f);
                Put(f, One("Skull1", "LargeBone_half01", "Pickable_ForestCryptRemains02"), fu, fv, R(0, 360), 0.3f);
                break;
            case "workshop":
                if (Put(f, _mat.Name == "Dvergr" ? "dvergrprops_table" : "piece_workbench", u, v, yaw + 90, 1.0f))
                {
                    Put(f, One("prop_piece_workbench_ext1", "prop_piece_workbench_ext2"), lu, lv, yaw, 0.6f);
                    Put(f, One("prop_wood_stack", "dvergrprops_pickaxe", "prop_piece_workbench_ext4"), ru, rv, yaw, 0.6f);
                }
                break;
        }
    }

    /// <summary>
    /// Up to n clusters in the room's corners (the convex ones, away from doorways), each a kind from the list in
    /// turn: the secondary groups that make a room read as lived in, beside its one main feature.
    /// </summary>
    private void Clusters(Frame f, int n, params string[] kinds)
    {
        int k = 0;
        foreach (var (u, v, yaw, convex) in f.Corners().Where(c => c.Convex).OrderBy(_ => _rnd.Next()))
        {
            if (k >= n)
            {
                break;
            }
            var (cu, cv) = In((u, v, yaw), 1.3f);
            if (f.NearDoor(cu, cv, 2.4f) || !f.Free(cu, cv, 0.9f))
            {
                continue;
            }
            Cluster(f, kinds[k % kinds.Length], cu, cv, yaw);
            k++;
        }
    }

    // ---- Hero pieces.
    /// <summary>
    /// The pieces a style's big room may be remembered by, each standing once in a dungeon: the fallen giant's statue
    /// in parts, a dolmen, a grave slab, a giant's skull or ribs in a warren, an altar of crystal in a hold, a cart in
    /// a keep. Null when the style's are all placed.
    /// </summary>
    private string? Hero()
    {
        // Each measured through the editor's models: nothing here is wider than 7 m or taller than a low room,
        // save the few marked for tall rooms.
        bool tall = _tallHero;
        string[] pool = _style.Name switch
        {
            "Crypt" or "Catacombs" => ["RockDolmen_1", "RockDolmen_2", "Morkhalla_StatuePieceFace", "GraveStone_Broken_World", "Morkhalla_StatuePieceTorso", "Morkhalla_coal_pile_memorial", "MemorialStone_Large", "stonechest", .. tall ? new[] { "highstone", "MemorialStone_Large" } : []],
            "Ruins" => ["Morkhalla_StatuePieceFace", "Morkhalla_StatuePieceTorso", "Morkhalla_StatuePieceFeet", "Morkhalla_StatuePieceArmL", "RockDolmen_3", "Ashlands_Pillar4_tip_broken1", "Ashlands_Ruins_twist_PillarBase", "Morkhalla_Rubble4", .. tall ? new[] { "Ashlands_Boss_Pillar" } : []],
            "Temple" => ["blackmarble_altar_crystal", "Morkhalla_StatuePieceFace", "RockDolmen_1", "Morkhalla_StatueSword", "piece_EternalPyre", "MemorialStone_Large", "widestone", .. tall ? new[] { "highstone" } : []],
            "Fortress" or "Prison" => ["Cart", "Morkhalla_StatueSword", "Morkhalla_StatuePieceSword", "Morkhalla_Trainingdummy2", "Sled", .. tall ? new[] { "piece_Charred_Balista" } : []],
            "Goblin warren" => ["Goblin_Gem", "piece_asksvinskeleton", "lox_ribs", "elaking_trashpile"],
            "Dvergr hold" => ["blackmarble_altar_crystal", "Pickable_BlackCoreStand", "blackmarble_head_big01", "blackmarble_head_big02", "piece_wisplure", "eitrrefinery", .. tall ? new[] { "dvergrtown_wood_crane" } : []],
            _ => ["RockDolmen_1", "Morkhalla_StatuePieceFace", "MemorialStone_Medium"],
        };
        var left = pool.Where(p => !_heroes.Contains(p)).ToList();
        if (left.Count == 0)
        {
            return null;
        }
        string hero = left[_rnd.Next(left.Count)];
        _heroes.Add(hero);
        return hero;
    }

    /// <summary>A hero piece at (u, v), turned yaw, keeping r metres; false when none is left or the spot is taken.</summary>
    private bool PutHero(Frame f, float u, float v, float yaw, float r = 2.2f)
    {
        _tallHero = f.S.Tall;
        if (!f.Free(u, v, r) || Hero() is not { } hero)
        {
            return false;
        }
        return Put(f, hero, u, v, yaw, r, force: true);
    }

    /// <summary>Whether the room a hero piece is being chosen for is tall (the tallest pieces only there).</summary>
    private bool _tallHero;

    // ---- Bare rooms and dead ends.
    /// <summary>
    /// Whether this room is left nearly bare: one thing in it and little else (a pacing beat between the full
    /// rooms, as the best dungeons keep a few). Never the rooms the way depends on, nor the key chest's.
    /// </summary>
    private bool Sparse(Frame f) =>
        f.S.Room is Room.Hall or Room.Gallery or Room.Burial or Room.Ossuary or Room.Tomb or Room.Store or Room.Den or Room.Barracks or Room.Hub or Room.Quarters
        && !f.S.KeyChest && Chance(0.08);

    /// <summary>
    /// A nearly bare room: one piece on its axis (a hero piece when one is left, else the style's lone thing: a
    /// sarcophagus, a statue, a totem, a table, a dead fire), a single light by it, cobwebs, a few bones; the dark
    /// elsewhere, a foe or two.
    /// </summary>
    private void Bare(Frame f)
    {
        float v = Chance(0.5) ? 0 : f.HalfD - 2.2f;
        if (!PutHero(f, 0, v, 180))
        {
            string lone = _style.Name switch
            {
                "Crypt" or "Catacombs" => One("crypt_skeleton_chest", "StatueEvil", "MountainGraveStone01", "bone_stack"),
                "Temple" => One("StatueFreya", "StatueThor", "StatueSeed"),
                "Goblin warren" => One("goblin_totempole", "lox_ribs", "goblin_strawpile"),
                "Dvergr hold" => One("dvergrprops_crate_long", "dvergrprops_table", "blackmarble_altar_crystal"),
                _ => One("piece_table", "Morkhalla_firepit", "StatueThor_broken_bottom", "piece_logbench01"),
            };
            Put(f, lone, 0, v, lone == "crypt_skeleton_chest" ? 90 : 180 + R(-15, 15), 1.2f, force: true);
        }
        if (Chance(0.6))
        {
            Brazier(f, R(-1, 1) + 2.2f, v - 0.4f);
        }
        else
        {
            Put(f, TorchOf(Local(f, 5)), -2.2f, v + 0.6f, 0, 0.4f);
        }
        Webs(f, 0.5f);
        Clutter(f, 0.006f);
        Foes(f, 0.4f);
        Decay();
    }

    /// <summary>
    /// A dead end's cache at its far end: the biome's chest against the end wall between a skull or a candle and
    /// a brazier, loot lying about; sometimes a guard.
    /// </summary>
    private void Cache(Frame f)
    {
        // Forced: a short dead end lies wholly within its doorway's clear space.
        float v = f.HalfD - 0.9f;
        Put(f, _b.Chest, 0, v, 180, 0.8f, force: true);
        Put(f, _s.Respawn ? "skull_pile" : "Candle_resin", -1.0f, v + 0.1f, R(0, 360), 0.3f, force: true);
        Put(f, _b.Brazier, 1.1f, v - 0.3f, 0, 0.6f, force: true);
        if (Chance(0.5))
        {
            Cores(f, 1);
        }
        Pickables(f, 1.2f);
        if (Chance(0.5))
        {
            Elite(f, 0, v - 2.4f, 0);
        }
    }

    // ---- Corridor kinds.
    /// <summary>
    /// What makes this corridor its own: a gate midway (a wall across with a door of the style's), a collapse (rubble
    /// and broken wall narrowing the way, a fallen pillar), bones along both walls (catacombs), a runner down the
    /// middle (a keep, a temple), vines on the walls (a crypt, ruins), or nothing but the torches. A long straight
    /// corridor only, never the bent ones. Then the biome's own: a muddy scrap pile to dig through in the swamp, a stake
    /// wall barricade or seeker creep in the mines, icicles and ice in the cold, spikes in the Ashlands, roots in the
    /// woods, a trap in a keep or a warren.
    /// </summary>
    private void CorridorKind(Frame f)
    {
        if (f.S.Shape != DungeonPlan.Shape.Rect || f.HalfD < 3)
        {
            return;
        }
        var kinds = new List<(string Kind, float Weight)> { ("plain", 2) };
        if (f.HalfD >= 4 && _mat.Name is not "Goblin")
        {
            kinds.Add(("gate", 1.2f));
        }
        if (_mat.Name is "Stone" or "Grausten" or "Marble")
        {
            // Chosen whatever the decay (so that the setting moves nothing else); a kept corridor then stays plain.
            kinds.Add(("collapse", 1.5f));
        }
        if (_style.Name is "Crypt" or "Catacombs" or "Ruins")
        {
            kinds.Add(("bones", _style.Name == "Catacombs" ? 2 : 1));
            kinds.Add(("vines", 0.8f));
        }
        if (_style.Name is "Fortress" or "Temple" or "Dvergr hold")
        {
            kinds.Add(("runner", 1.2f));
        }
        if (_style.Name is "Goblin warren")
        {
            kinds.Add(("stakes", 1));
        }
        // The biome's own: mud in the swamp's crypts, a barricade or creep in the mines, icicles in the cold,
        // spikes in the Ashlands, roots in the woods, a trap in a keep or a warren.
        switch (_b.Name)
        {
            case "Swamp":
                kinds.Add(("mud", 1.5f));
                kinds.Add(("roots", 1));
                break;
            case "Mistlands":
                kinds.Add(("barricade", 1.2f));
                kinds.Add(("creep", 1.2f));
                break;
            case "Mountain" or "Deep North":
                kinds.Add(("icicles", 1.5f));
                break;
            case "Ashlands":
                kinds.Add(("spikes", 1));
                break;
            case "Meadows" or "Black Forest":
                kinds.Add(("roots", 1));
                break;
        }
        if (_style.Name is "Fortress" or "Prison" or "Goblin warren" && f.HalfD >= 4)
        {
            kinds.Add(("trap", 0.8f));
        }
        float total = kinds.Sum(k => k.Weight), pick = R(0, total);
        string kind = kinds.First(k => (pick -= k.Weight) <= 0).Kind;
        switch (kind)
        {
            case "gate":
                CorridorGate(f);
                break;
            case "collapse":
                CorridorCollapse(f);
                break;
            case "bones":
                for (float v = -f.HalfD + 2; v <= f.HalfD - 2; v += 2)
                {
                    foreach (int side in new[] { -1, 1 })
                    {
                        float u = side * (f.HalfA - 0.55f);
                        // Against the walls, whatever else keeps the floor (a corridor is narrow: its doorways' clear
                        // space would refuse nearly every spot), only not right by a doorway.
                        if (!f.NearDoor(u, v, 2.2f))
                        {
                            Put(f, One("skull_pile", "bone_stack", "skull_pile", "LargeBone"), u, v, R(0, 360), 0.4f, force: true);
                        }
                    }
                }
                break;
            case "runner":
                Runner(f, -f.HalfD + 1, f.HalfD - 1);
                break;
            case "vines":
                foreach (var sp in f.WallSpots(3, 2.2f).Where(sp => sp.Yaw is 90 or -90).Where(_ => Chance(0.6)))
                {
                    var (vu, vv) = In(sp, 0.1f);
                    _o.Add("vines", f.W(vu, vv, f.Top + 2.3f + R(-0.3f, 0.5f)), f.Yaw(sp.Yaw + 180));
                }
                break;
            case "mud":
                // A muddy scrap pile across the way, to be dug through, as in the sunken crypts.
                Put(f, "mudpile", 0, R(-1, 1), R(0, 360), 1.4f, force: true);
                Put(f, One("Pickable_Tar", "skull_pile", "LargeBone"), R(-0.8f, 0.8f), R(2.2f, 3.2f), R(0, 360), 0.3f);
                break;
            case "barricade":
                // The miners' stake wall across the way, to be hacked through.
                Put(f, "dvergrtown_wood_stakewall", 0, R(-1, 1), 90, 1.4f, force: true);
                break;
            case "creep":
                foreach (var sp in f.WallSpots(2.4f, 2.2f).Where(sp => sp.Yaw is 90 or -90).Where(_ => Chance(0.6)))
                {
                    var (cu, cv) = In(sp, 0.15f);
                    _o.Add("CreepProp_wall01", f.W(cu, cv, f.Top + 1.6f), f.Yaw(sp.Yaw));
                }
                if (Spot(f, 0.4f, 1.2f) is var (eu, ev))
                {
                    _o.Add(One("CreepProp_egg_hanging01", "CreepProp_egg_hanging02"), f.W(eu, ev, f.Top + f.H - 2.8f), f.Yaw(R(0, 360)));
                }
                Put(f, "SeekerEgg", R(-0.8f, 0.8f), R(-f.HalfD + 2, f.HalfD - 2), R(0, 360), 0.3f);
                break;
            case "icicles":
                for (float v = -f.HalfD + 2; v <= f.HalfD - 2; v += R(1.5f, 3f))
                {
                    _o.Add("caverock_ice_stalagtite", f.W(R(-1, 1), v, f.Top + f.H - 3.4f), f.Yaw(R(0, 360)));
                }
                foreach (int side in new[] { -1, 1 })
                {
                    Put(f, "BlackIceShard_01", side * (f.HalfA - 0.5f), R(-f.HalfD + 2, f.HalfD - 2), R(0, 360), 0.3f);
                }
                break;
            case "spikes":
                foreach (var sp in f.WallSpots(4, 2.6f).Where(sp => sp.Yaw is 90 or -90))
                {
                    var (su2, sv2) = In(sp, 0.2f);
                    _o.Add("Ashlands_Fortress_Wall_Spikes", f.W(su2, sv2, f.Top + 1.9f), f.Yaw(sp.Yaw + 90));
                }
                break;
            case "roots":
                foreach (var sp in f.WallSpots(2.2f, 2.4f).Where(sp => sp.Yaw is 90 or -90).Where(_ => Chance(0.6)))
                {
                    var (ru2, rv2) = In(sp, 0.7f);
                    Put(f, One("root11", "root12", "root07"), ru2, rv2, R(0, 360), 0.6f, force: true);
                }
                break;
            case "trap":
                Put(f, _style.Name == "Goblin warren" ? "fuling_trap" : "piece_trap_troll", 0, R(-1.5f, 1.5f), 90, 0.9f, force: true);
                break;
            case "stakes":
                foreach (int side in new[] { -1, 1 })
                {
                    for (float v = -f.HalfD + 2.5f; v <= f.HalfD - 2.5f; v += 3)
                    {
                        float u = side * (f.HalfA - 0.9f);
                        if (!f.NearDoor(u, v, 2.4f))
                        {
                            Put(f, "piece_sharpstakes", u, v, side < 0 ? 90 : -90, 0.6f, force: true);
                        }
                    }
                }
                break;
        }
    }

    /// <summary>A wall across the corridor at its middle, 3 m high, with the style's door in the middle of it.</summary>
    private void CorridorGate(Frame f)
    {
        float v = R(-1, 1);
        if (f.NearDoor(-f.HalfA, v, 3) || f.NearDoor(f.HalfA, v, 3))
        {
            return;
        }
        // A corridor is 3 m wide within its walls: no room for jambs beside a 2 m door, so the gate is the door alone
        // under a band of wall from wall to wall (held by both; it may rise into the roof).
        InnerWall(f, -f.HalfA, v, f.HalfA, v, 3, f.H);
        string door = _style.Name switch
        {
            "Fortress" or "Temple" => "dungeon_forestcrypt_door",
            "Dvergr hold" => "dvergrtown_slidingdoor",
            _ => One("dungeon_sunkencrypt_irongate", "dungeon_sunkencrypt_irongate_rusty"),
        };
        _o.Add(door, f.W(0, v, f.F), f.Yaw(0));
        f.Take(0, v, 1.6f);
        f.Take(-(f.HalfA - 1), v, 1.2f);
        f.Take(f.HalfA - 1, v, 1.2f);
    }

    /// <summary>
    /// A collapse: a heap of rubble and broken wall against one side, a pillar fallen across half the way, stones
    /// about; the way stays open along the other side.
    /// </summary>
    private void CorridorCollapse(Frame f)
    {
        if (_decay <= 0.2f && _style.Name != "Ruins")
        {
            return;
        }
        // Decay's own stream: the Decay setting moves nothing else.
        var rnd = Local(f, 4);
        float Rr(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
        string Pick(params string[] list) => list[rnd.Next(list.Length)];
        float v = Rr(-f.HalfD + 3, f.HalfD - 3);
        int side = rnd.Next(2) == 0 ? -1 : 1;
        if (f.NearDoor(side * f.HalfA, v, 3))
        {
            return;
        }
        float u = side * (f.HalfA - 1.1f);
        Put(f, Pick("Morkhalla_Rubble2", "Morkhalla_Rubble3", "Morkhalla_Rubble1"), u, v, Rr(0, 360), 1.0f, force: true);
        Put(f, Pick("stone_wall_2x1_ruin", "stone_wall_1x1_ruin"), u + side * 0.3f, v + 1.4f, Rr(-20, 20) + (side < 0 ? 90 : -90), 0.6f);
        Put(f, "Morkhalla_Rubble1", u - side * 0.6f, v - 1.5f, Rr(0, 360), 0.6f);
        if (_mat.Column[0].Prefab == "stone_pillar" && rnd.NextDouble() < 0.6)
        {
            var q = DungeonOut.Yaw(f.Yaw(Rr(60, 120))) * Quaternion.CreateFromAxisAngle(Vector3.UnitX, MathF.PI / 2);
            _o.Add("stone_pillar", f.W(side * (f.HalfA - 2.1f), v - 0.4f, f.F + 0.55f), q);
            f.Take(side * (f.HalfA - 2.1f), v - 0.4f, 1.3f);
        }
        for (int i = 0; i < 3; i++)
        {
            Put(f, Pick("Pickable_StoneRock", "piece_pot1_cracked", "stone_wall_1x1_ruin", "Morkhalla_Rubble1"), u - side * Rr(0.8f, 2.2f), v + Rr(-2.4f, 2.4f), Rr(0, 360), 0.3f);
        }
    }
}
