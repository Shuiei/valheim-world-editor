using WorldEditor.Tests.Unity;

namespace WorldEditor.Tests;

/// <summary>
/// A made-up game for the readers of the game's data at the editor's start (GameCatalogues): build pieces with their
/// colliders, costs and menus, networked prefabs of every kind the prefabs catalogue tells apart, terrain modifiers on
/// a location and a networked prefab, a dungeon with a room, the English texts. Its scripts are in a bundle of their
/// own (as the game's are), referenced from the others.
/// </summary>
public static class CatalogueGame
{
    /// <summary>
    /// Unity layers: the build ray's "piece", and one it does not see ("character").
    /// </summary>
    public const uint PieceLayer = 10, CharacterLayer = 9;

    /// <summary>
    /// The game's "snappoint" tag.
    /// </summary>
    public const int SnapTag = 20000;

    /// <summary>
    /// A turn of 90° about y (x, y, z, w).
    /// </summary>
    public static readonly (double X, double Y, double Z, double W) Quarter = (0, Math.Sqrt(0.5), 0, Math.Sqrt(0.5));

    /// <summary>
    /// The game: scripts, items, pieces, prefabs, locations and a dungeon in their bundles, and the English texts.
    /// </summary>
    public static TestGame Make()
    {
        var game = new TestGame();
        var scripts = game.Bundle("a_scripts");
        // Scripts made first, so every bundle finds them.
        foreach (string c in GameScripts.Fields.Keys)
        {
            scripts.MonoScript(c);
        }
        long sword = Items(game.Bundle("b_items", scripts));
        long wood = Pieces(game.Bundle("c_pieces", scripts));
        Prefabs(game.Bundle("d_prefabs", scripts), sword, wood);
        Locations(game.Bundle("e_locations", scripts));
        Dungeons(game.Bundle("f_dungeons", scripts));
        game.Resources([
            ("localization_english", "English,Name\nitem_wood,Wood\npiece_woodwall,\"Wood wall, sturdy\"\npiece_workbench,Workbench\n\"quoted\"\"key\",x\n"),
            ("readme", "item_wood,Not this\n"),
        ]);
        return game;
    }

    /// <summary>
    /// A bundle of the game whose scripts are in <paramref name="scripts"/>.
    /// </summary>
    private static TestBundle Bundle(this TestGame game, string name, TestBundle scripts)
    {
        var b = game.Bundle(name);
        b.ScriptsFrom = scripts;
        return b;
    }

    /// <summary>
    /// A networked item: its drop's shared data, and a child of this name ("attach", "attach_skin") when given.
    /// Returns the ItemDrop's path id.
    /// </summary>
    private static long Item(TestBundle b, string name, Fields shared, string? child = null)
    {
        long drop = 0;
        b.Prefab(name, r =>
        {
            r.Mono("ZNetView", new Fields { ["m_persistent"] = true, ["m_type"] = 0 });
            drop = r.Mono("ItemDrop", new Fields { ["m_autoPickup"] = true, ["m_itemData"] = new Fields { ["m_shared"] = shared } });
            if (child != null)
            {
                r.Child(child);
            }
        });
        return drop;
    }

    /// <summary>
    /// The items bundle: a sword that wears out (an armour stand's "attach_skin" on it). Returns its ItemDrop.
    /// </summary>
    private static long Items(TestBundle b) =>
        Item(b, "Sword", new Fields { ["m_name"] = "$item_sword", ["m_itemType"] = 3, ["m_attachOverride"] = 5, ["m_useDurability"] = true, ["m_maxDurability"] = 200.0, ["m_durabilityPerLevel"] = 50.0 }, "attach_skin");

    /// <summary>
    /// The pieces bundle: a wood wall (box, snap points, sphere, capsule and mesh colliders, a trigger and a disabled
    /// box left out), a stone floor not networked, a workbench (a crafting station), the wood they cost (an item with
    /// variants), the hammer's and the hoe's menus. Returns the wood's ItemDrop.
    /// </summary>
    private static long Pieces(TestBundle b)
    {
        long wood = Item(b, "Wood", new Fields { ["m_name"] = "$item_wood", ["m_itemType"] = 1, ["m_variants"] = 2 }, "attach");
        long station = 0;
        var bench = b.Prefab("piece_workbench", r =>
        {
            r.Mono("ZNetView", new Fields { ["m_persistent"] = true });
            r.Mono("Piece", new Fields { ["m_name"] = "$piece_workbench", ["m_category"] = 1, ["m_resources"] = new List<Fields> { Cost(wood, 10, true) } });
            station = r.Mono("CraftingStation", new Fields { ["m_name"] = "$piece_workbench", ["m_rangeBuild"] = 20.0 });
        });
        long mesh = new TestMesh { Name = "wall_collider", Positions = [0, 0, 0, 1, 0, 0, 0, 1, 0], Submeshes = [[0, 1, 2]], Bounds = ((0.5, 0.5, 0), (0.5, 0.5, 0)) }.Add(b);
        var wall = b.Prefab("wood_wall", r =>
        {
            r.Layer = PieceLayer;
            r.Mono("ZNetView", new Fields { ["m_persistent"] = true });
            r.Mono("Piece", new Fields
            {
                ["m_name"] = "$piece_woodwall",
                ["m_category"] = 2,
                ["m_comfort"] = 1,
                ["m_groundPiece"] = true,
                ["m_noInWater"] = true,
                ["m_craftingStation"] = Fields.Ref(station),
                ["m_resources"] = new List<Fields> { Cost(wood, 4, true), Cost(wood, 0, true), Cost(0, 2, false) },
            });
            r.Mono("WearNTear", new Fields { ["m_materialType"] = 0, ["m_supports"] = true, ["m_comOffset"] = Fields.Vector3(0, 1, 0), ["m_noSupportWear"] = true, ["m_health"] = 400.0 });
            r.Box((0, 1, 0), (2, 2, 0.2));
            r.Box((0, 5, 0), (1, 1, 1), trigger: true);
            r.Box((0, 6, 0), (1, 1, 1), enabled: false);
            r.Child("snappoint_top", c =>
            {
                c.Tag = SnapTag;
                c.Active = false;
                c.Position = (1, 2, 0);
            });
            r.Child("parts", c =>
            {
                c.Layer = PieceLayer;
                c.Position = (0, 2, 0);
                c.Rotation = Quarter;
                c.Sphere((1, 0, 0), 0.5);
                c.Capsule((0, 0, 0), 0.25, 2, 1);
                c.MeshCollider(mesh, convex: true);
            });
            r.Child("ghost", c =>
            {
                c.Layer = CharacterLayer;
                c.Box((0, 0, 0), (1, 1, 1));
            });
            r.Child("off", c =>
            {
                c.Active = false;
                c.Box((0, 0, 0), (9, 9, 9));
            });
        });
        var floor = b.Prefab("stone_floor", r =>
        {
            r.Scale = (2, 1, 2);
            r.Mono("Piece", new Fields { ["m_category"] = 0, ["m_resources"] = new List<Fields>() });
            r.Mono("WearNTear", new Fields { ["m_materialType"] = 1, ["m_supports"] = false });
            r.Box((0, 0, 0), (1, 0.2, 1));
        });
        Table(b, "_HammerPieceTable", [Fields.Ref(wall.Go), Fields.Ref(floor.Go), Fields.Ref(bench.Go), Fields.Ref(99999)]);
        Table(b, "_HoePieceTable", [Fields.Ref(wall.Go)]);
        Table(b, "_NotATool", [Fields.Ref(floor.Go)]);
        return wood;
    }

    /// <summary>
    /// A cost: an ItemDrop (path id in the same file), how many, whether it is given back.
    /// </summary>
    private static Fields Cost(long drop, int amount, bool recover) => new() { ["m_resItem"] = Fields.Ref(drop), ["m_amount"] = amount, ["m_recover"] = recover };

    /// <summary>
    /// A build tool's menu: a PieceTable on an object of this name (not a prefab: under no table of contents entry).
    /// </summary>
    private static void Table(TestBundle b, string name, List<Fields> pieces) =>
        b.Prefab(name, r => r.Mono("PieceTable", new Fields { ["m_pieces"] = pieces }), path: $"assets/tables/{name}.asset");

    /// <summary>
    /// The prefabs bundle: a creature, a chest, a ward, a sapling and the tree it grows into, an item stand and an
    /// armour stand (what they take: the wood of the pieces' bundle, not the sword of the items'), an object that is not
    /// networked.
    /// </summary>
    private static void Prefabs(TestBundle b, long sword, long wood)
    {
        b.Prefab("Boar", r =>
        {
            r.Mono("ZNetView", new Fields { ["m_persistent"] = true, ["m_distant"] = false, ["m_type"] = 2 });
            r.Mono("Character", new Fields { ["m_runSpeed"] = 6.0 });
        });
        b.Prefab("piece_chest", r =>
        {
            r.Mono("ZNetView", new Fields { ["m_persistent"] = true });
            r.Mono("Container", new Fields { ["m_width"] = 5, ["m_height"] = 2 });
        });
        b.Prefab("guard_stone", r =>
        {
            r.Mono("ZNetView", new Fields { ["m_persistent"] = true, ["m_distant"] = true });
            r.Mono("PrivateArea", new Fields { ["m_radius"] = 32.0 });
        });
        var tree = b.Prefab("Beech", r => r.Mono("ZNetView", new Fields { ["m_persistent"] = true, ["m_type"] = 1 }));
        b.Prefab("Beech_Sapling", r =>
        {
            r.Mono("ZNetView", new Fields { ["m_persistent"] = true });
            r.Mono("Plant", new Fields { ["m_grownPrefabs"] = new List<Fields> { Fields.Ref(tree.Go), Fields.Ref(tree.Go) }, ["m_growRadius"] = 1.5, ["m_needCultivatedGround"] = true });
        });
        b.Prefab("itemstand", r =>
        {
            r.Mono("ZNetView", new Fields { ["m_persistent"] = true });
            r.Mono("ItemStand", new Fields
            {
                ["m_supportedTypes"] = new List<object> { 3, 1, 3 },
                ["m_attachOther"] = Fields.Ref(0),
                ["m_supportedItems"] = new List<Fields> { Fields.Ref(wood, b.Assets.External("archive:/CAB-c_pieces/CAB-c_pieces")) },
                ["m_unsupportedItems"] = new List<Fields> { Fields.Ref(sword, b.Assets.External("archive:/CAB-b_items/CAB-b_items")) },
            });
        });
        b.Prefab("ArmorStand", r =>
        {
            r.Mono("ZNetView", new Fields { ["m_persistent"] = true });
            r.Mono("ArmorStand", new Fields { ["m_slots"] = new List<Fields> { new() { ["m_supportedTypes"] = new List<object> { 7, 6 } }, new() { ["m_supportedTypes"] = new List<object> { 11 } } }, ["m_poseCount"] = 3 });
        });
        b.Prefab("Prop");
    }

    /// <summary>
    /// The locations bundle: a location with modifiers on its children (one inactive, one disabled), a networked kiln
    /// with one on its root, and a prop that is neither (its modifier not kept).
    /// </summary>
    private static void Locations(TestBundle b)
    {
        b.Prefab("Village", r =>
        {
            r.Mono("Location", new Fields { ["m_exteriorRadius"] = 20.0, ["m_clearArea"] = true, ["m_applyRandomDamage"] = false, ["m_hasInterior"] = false });
            r.Child("houses", c =>
            {
                c.Position = (10, 0, 0);
                c.Rotation = Quarter;
                c.Child("yard", y =>
                {
                    y.Position = (2, 1, 0);
                    y.Mono("TerrainModifier", Modifier(1, level: true));
                });
                c.Child("old", y =>
                {
                    y.Active = false;
                    y.Mono("TerrainModifier", Modifier(2));
                });
            });
            r.Child("well", c =>
            {
                c.Mono("TerrainModifier", Modifier(3), enabled: false);
                c.Mono("TerrainModifier", Modifier(4, level: false));
            });
            // A networked object of its own (a saved object when the location is spawned), named as an instance.
            r.Child("Shrine (1)", c =>
            {
                c.Position = (0, 0, -3);
                c.Mono("ZNetView", new Fields { ["m_persistent"] = true });
            });
        });
        b.Prefab("Kiln", r =>
        {
            r.Mono("ZNetView", new Fields { ["m_persistent"] = true });
            r.Mono("TerrainModifier", Modifier(5, level: true));
        });
        b.Prefab("Prop_rock", r => r.Mono("TerrainModifier", Modifier(6)));
        // A game script mods' extensions read (ModComponents): on a prefab's root, and on a child of another.
        b.Prefab("paved_test", r => r.Mono("TerrainOp", new Fields
        {
            ["m_settings"] = new Fields { ["m_paintCleared"] = true, ["m_paintType"] = 2, ["m_paintRadius"] = 2.2 },
        }));
        b.Prefab("raise_test", r => r.Child("raise_op", c => c.Mono("TerrainOp", new Fields
        {
            ["m_settings"] = new Fields { ["m_levelOffset"] = 0.5, ["m_raise"] = true, ["m_raiseDelta"] = 1.0 },
        })));
    }

    /// <summary>
    /// A terrain modifier's fields, told apart by its sort order.
    /// </summary>
    private static Fields Modifier(int order, bool level = false) => new()
    {
        ["m_sortOrder"] = order,
        ["m_level"] = level,
        ["m_levelRadius"] = 4.5,
        ["m_levelOffset"] = 0.25,
        ["m_smooth"] = true,
        ["m_smoothRadius"] = 6.0,
        ["m_smoothPower"] = 3.0,
        ["m_paintCleared"] = true,
        ["m_paintType"] = 1,
        ["m_paintRadius"] = 5.0,
        ["m_paintStrength"] = 0.5,
        ["m_playerModifiction"] = false,
    };

    /// <summary>
    /// The dungeons bundle: a dungeon generator with a door, and a room with an opening, an object it makes, a random
    /// spawn with what shows instead, a random pick of two, and an inactive part left out.
    /// </summary>
    private static void Dungeons(TestBundle b)
    {
        var door = b.Prefab("dungeon_door");
        b.Prefab("DG_Test", r => r.Mono("DungeonGenerator", new Fields
        {
            ["m_algorithm"] = 1,
            ["m_maxRooms"] = 10,
            ["m_minRooms"] = 3,
            ["m_themes"] = 2,
            ["m_doorTypes"] = new List<Fields>
            {
                new() { ["m_prefab"] = Fields.Ref(door.Go), ["m_connectionType"] = "crypt_door", ["m_chance"] = 0.0 },
                new() { ["m_prefab"] = Fields.Ref(0), ["m_connectionType"] = "none" },
            },
            ["m_doorChance"] = 0.5,
            ["m_tileWidth"] = 8.0,
            ["m_zoneSize"] = Fields.Vector3(64, 64, 64),
        }));
        b.Prefab("crypt_room", r =>
        {
            r.Mono("Room", new Fields { ["m_theme"] = 2, ["m_size"] = new Fields { ["x"] = 8, ["y"] = 4, ["z"] = 8 }, ["m_weight"] = 2.0, ["m_enabled"] = true });
            r.Child("door_n", c =>
            {
                c.Position = (0, 0, 4);
                c.Mono("RoomConnection", new Fields { ["m_type"] = "crypt_door", ["m_entrance"] = true, ["m_allowDoor"] = true });
            });
            r.Child("Chest (1)", c =>
            {
                c.Position = (1, 0, 1);
                c.Rotation = Quarter;
                c.Mono("ZNetView", new Fields { ["m_persistent"] = true });
            });
            var instead = r.Child("rubble");
            r.Child("maybe", c =>
            {
                c.Mono("RandomSpawn", new Fields { ["m_OffObject"] = Fields.Ref(instead.Go), ["m_chanceToSpawn"] = 50.0, ["m_minElevation"] = -10000.0, ["m_maxElevation"] = 10000.0 });
                c.Child("Torch", t => t.Mono("ZNetView", new Fields { ["m_persistent"] = true }));
            });
            var a = r.Child("a");
            var bb = r.Child("b");
            r.Child("pick", c => c.Mono("RandomObject", new Fields
            {
                ["m_objects"] = new List<Fields> { new() { ["m_object"] = Fields.Ref(a.Go), ["m_weight"] = 1.0 }, new() { ["m_object"] = Fields.Ref(bb.Go), ["m_weight"] = 3.0 } },
                ["m_minElevation"] = -10000.0,
                ["m_maxElevation"] = 10000.0,
            }));
            r.Child("hidden", c =>
            {
                c.Active = false;
                c.Mono("ZNetView", new Fields { ["m_persistent"] = true });
            });
            r.Child("Spare", c => c.Mono("ZNetView", new Fields { ["m_persistent"] = true }, enabled: false));
        });
    }
}
