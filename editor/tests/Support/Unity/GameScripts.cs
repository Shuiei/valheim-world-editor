using static WorldEditor.Tests.Unity.UnityField;

namespace WorldEditor.Tests.Unity;

/// <summary>
/// The fields of the game's scripts that the editor's readers look for, by class name, as the test files describe
/// them: only the fields read (each reader tells a script by its class or by these names), with types the readers
/// take. Made up here from what the readers read; nothing is copied from the game.
/// </summary>
public static class GameScripts
{
    /// <summary>
    /// A reference list (of GameObjects or components).
    /// </summary>
    private static UnityField Refs(string name) => List(name, Ref("data"));

    /// <summary>
    /// A list of ints.
    /// </summary>
    private static UnityField Ints(string name) => List(name, Of("int", "data"));

    /// <summary>
    /// A yes/no padded to 4 bytes, as the game's bool fields are.
    /// </summary>
    private static UnityField Bool(string name) => Of("bool", name, align: true);

    /// <summary>
    /// A location rule's fields (ZoneLocation): its prefab by asset id, then the rule's numbers and names.
    /// </summary>
    public static readonly UnityField ZoneLocation = Struct("ZoneLocation", "data",
        Bool("m_enable"),
        Text("m_name"),
        Text("m_prefabName"),
        Struct("SoftReference", "m_prefab", Struct("AssetID", "m_assetID", Of("unsigned int", "v0"), Of("unsigned int", "v1"), Of("unsigned int", "v2"), Of("unsigned int", "v3"))),
        Of("int", "m_biome"),
        Of("int", "m_quantity"),
        Bool("m_prioritized"),
        Bool("m_iconAlways"),
        Bool("m_unique"),
        Text("m_group"),
        Of("float", "m_minDistanceFromSimilar"),
        Of("float", "m_exteriorRadius"),
        Of("float", "m_minAltitude"),
        Of("float", "m_maxAltitude"));

    /// <summary>
    /// A vegetation rule's fields (ZoneVegetation): a few of the kept ones and its prefab.
    /// </summary>
    public static readonly UnityField ZoneVegetation = Struct("ZoneVegetation", "data",
        Text("m_name"),
        Ref("m_prefab"),
        Bool("m_enable"),
        Of("float", "m_min"),
        Of("float", "m_max"),
        Of("int", "m_biome"),
        Bool("m_inForest"));

    /// <summary>
    /// Each script's fields, by class name.
    /// </summary>
    public static readonly Dictionary<string, UnityField[]> Fields = new(StringComparer.Ordinal)
    {
        ["ZNetView"] = [Bool("m_persistent"), Bool("m_distant"), Of("int", "m_type")],
        ["Piece"] =
        [
            Text("m_name"),
            Of("int", "m_category"),
            Of("int", "m_comfort"),
            Bool("m_groundPiece"),
            Bool("m_clipGround"),
            Bool("m_clipEverything"),
            Bool("m_allowRotatedOverlap"),
            Bool("m_waterPiece"),
            Bool("m_noInWater"),
            Ref("m_craftingStation"),
            List("m_resources", Struct("Requirement", "data", Ref("m_resItem"), Of("int", "m_amount"), Bool("m_recover"))),
        ],
        ["WearNTear"] = [Ref("m_new"), Ref("m_worn"), Ref("m_broken"), Bool("m_noSupportWear"), Bool("m_noRoofWear"), Of("float", "m_health"), Bool("m_supports"), Vector3("m_comOffset"), Of("int", "m_materialType")],
        ["PieceTable"] = [Refs("m_pieces")],
        ["ItemDrop"] =
        [
            Bool("m_autoPickup"),
            Struct("ItemData", "m_itemData", Struct("SharedData", "m_shared",
                Text("m_name"),
                Of("int", "m_itemType"),
                Of("int", "m_attachOverride"),
                Of("int", "m_variants"),
                Bool("m_useDurability"),
                Of("float", "m_maxDurability"),
                Of("float", "m_durabilityPerLevel"),
                Of("float", "m_scaleByQuality"),
                List("m_itemStandOffsets", Struct("OrientationSettings", "data", Vector3("m_position"), Vector3("m_rotation"), Vector3("m_scale"), Of("int", "m_orientations"))))),
        ],
        ["CraftingStation"] = [Text("m_name"), Of("float", "m_rangeBuild")],
        ["Character"] = [Of("float", "m_runSpeed")],
        ["Plant"] = [Refs("m_grownPrefabs"), Of("float", "m_growRadius"), Bool("m_needCultivatedGround")],
        ["Container"] = [Of("int", "m_width"), Of("int", "m_height"), Of("int", "m_privacy")],
        ["PrivateArea"] = [Of("float", "m_radius"), Ref("m_areaMarker"), Struct("EffectList", "m_enabledEffect", Of("int", "m_count"))],
        ["ItemStand"] = [Ints("m_supportedTypes"), Ref("m_attachOther"), Of("int", "m_orientationType"), Refs("m_supportedItems"), Refs("m_unsupportedItems")],
        ["ArmorStand"] = [List("m_slots", Struct("ArmorStandSlot", "data", Of("int", "m_slot"), Ints("m_supportedTypes"))), Ref("m_visEquipment"), Of("int", "m_poseCount")],
        ["VisEquipment"] = [Ref("m_bodyModel"), Ref("m_helmet"), Ref("m_leftHand"), Ref("m_rightHand"), Ref("m_backShield"), Ref("m_backMelee"), Ref("m_backTwohandedMelee"), Ref("m_backBow"), Ref("m_backTool"), Ref("m_backAtgeir")],
        ["TerrainModifier"] =
        [
            Of("int", "m_sortOrder"),
            Bool("m_useTerrainCompiler"),
            Bool("m_playerModifiction"),
            Bool("m_level"),
            Of("float", "m_levelRadius"),
            Of("float", "m_levelOffset"),
            Bool("m_square"),
            Bool("m_smooth"),
            Of("float", "m_smoothRadius"),
            Of("float", "m_smoothPower"),
            Bool("m_paintCleared"),
            Bool("m_paintHeightCheck"),
            Of("int", "m_paintType"),
            Of("float", "m_paintRadius"),
            Of("float", "m_paintStrength"),
        ],
        ["TerrainOp"] =
        [
            Struct("Settings", "m_settings",
                Of("float", "m_levelOffset"),
                Bool("m_level"),
                Of("float", "m_levelRadius"),
                Bool("m_raise"),
                Of("float", "m_raiseDelta"),
                Bool("m_paintCleared"),
                Of("int", "m_paintType"),
                Of("float", "m_paintRadius")),
        ],
        ["Location"] = [Of("float", "m_exteriorRadius"), Bool("m_clearArea"), Bool("m_applyRandomDamage"), Bool("m_hasInterior")],
        ["Room"] =
        [
            Of("int", "m_theme"),
            Bool("m_entrance"),
            Bool("m_endCap"),
            Bool("m_divider"),
            Of("int", "m_endCapPrio"),
            Of("float", "m_weight"),
            Bool("m_perimeter"),
            Struct("Vector3Int", "m_size", Of("int", "x"), Of("int", "y"), Of("int", "z")),
            Bool("m_enabled"),
        ],
        ["RoomConnection"] = [Text("m_type"), Bool("m_entrance"), Bool("m_allowDoor"), Bool("m_doorOnlyIfOtherAlsoAllowsDoor")],
        ["RandomSpawn"] = [Ref("m_OffObject"), Of("float", "m_chanceToSpawn"), Of("int", "m_dungeonRequireTheme"), Of("float", "m_minElevation"), Of("float", "m_maxElevation")],
        ["RandomObject"] = [List("m_objects", Struct("Choice", "data", Ref("m_object"), Of("float", "m_weight"))), Of("int", "m_dungeonRequireTheme"), Of("float", "m_minElevation"), Of("float", "m_maxElevation")],
        ["DungeonGenerator"] =
        [
            Of("int", "m_algorithm"),
            Of("int", "m_maxRooms"),
            Of("int", "m_minRooms"),
            Of("int", "m_themes"),
            List("m_doorTypes", Struct("DoorDef", "data", Ref("m_prefab"), Text("m_connectionType"), Of("float", "m_chance"))),
            Of("float", "m_doorChance"),
            Of("float", "m_tileWidth"),
            Vector3("m_zoneSize"),
            Bool("m_useCustomInteriorTransform"),
            Bool("m_addBaseSeedToRandomSpawn"),
        ],
        ["ZoneSystem"] = [Of("float", "m_zoneSize"), List("m_vegetation", ZoneVegetation), List("m_locations", ZoneLocation)],
        ["LocationList"] = [Of("int", "m_sortOrder"), List("m_vegetation", ZoneVegetation), List("m_locations", ZoneLocation)],
        ["WorldGenerator"] =
        [
            List("m_alts", Struct("AltBiome", "data", Text("m_name"), Bool("m_enabled"), Of("int", "m_biome"), List("m_addVegetation", ZoneVegetation),
                List("m_blockVegetationNames", Text("data")), List("m_addLocations", ZoneLocation), List("m_blockLocationNames", Text("data")),
                Of("float", "m_minDistanceFromCenter"), Of("int", "m_minAmountSpawned"), Of("int", "m_maxAmountSpawned"), Of("float", "m_chance"),
                List("m_incompatibleAltBiomes", Text("data")))),
        ],
        ["Pickable"] = [Of("float", "m_respawnTimeMinutes")],
        ["LodFadeInOut"] = [Of("float", "m_fadeTime")],
        ["MineRock5"] = [Of("float", "m_health")],
        ["RandomFlyingBird"] = [Of("float", "m_flySpeed")],
        ["Floating"] = [Of("float", "m_waterLevelOffset")],
    };

    /// <summary>
    /// A MonoBehaviour type tree for a script class of <c>Fields</c>.
    /// </summary>
    public static UnityField Tree(string className) => UnityTypes.MonoBehaviour(Fields[className]);
}
