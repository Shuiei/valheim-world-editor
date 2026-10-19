using AssetsTools.NET;
using Dict = TerrainEditor.App.CatalogueJson.Dict;

namespace TerrainEditor.App;

public static partial class GameCatalogues
{
    /// <summary>
    /// The game's vegetation rules, ZoneSystem's m_vegetation then the LocationLists' (by m_sortOrder), as
    /// the game merges them; "Regrow nature" places by these rules. Read at the editor's start (GameData):
    /// only the main scene's bundles, where ZoneSystem and the lists are (as for GameLocations), then the
    /// prefabs' and scripts' bundles. The alt biomes' added vegetation comes last, in the alt biomes' order, each
    /// rule with its alt biome's name ("altBiome": it grows only in that alt biome's areas). "draws":
    /// the random draws the prefab's components make in Awake when the game creates it (they shift the
    /// generator's sequence for the rest of the rule): LodFadeInOut (with a LODGroup: the camera is more
    /// than 20 m away while zones generate around a player), MineRock5, Floating, LightFlicker, Fish one
    /// each, RandomFlyingBird two, Pickable one when it respawns (m_respawnTimeMinutes &gt; 0). Checked
    /// against a played world: with these, the generated spots match the saved ones. Components of
    /// inactive children do not wake.
    /// </summary>
    public static string VegetationJson(GameBundles bundles)
    {
        using var game = new Game(bundles.Folder);
        // For the prefabs, in bundles not opened yet.
        game.IndexCabs();
        var scan = new VegetationScan();
        foreach (string name in GameLocations.MainSceneBundles(bundles).Order(StringComparer.Ordinal))
        {
            using var b = game.Open(name) ?? throw new InvalidDataException($"{name} cannot be read");
            scan.Read(b);
        }
        if (!scan.Found)
        {
            throw new InvalidDataException("the main scene's ZoneSystem was not found");
        }
        return CatalogueJson.Write(scan.Finish(game), new CatalogueJson.Style(",", Indent: 0));
    }

    /// <summary>
    /// Gathers the vegetation rules of the main scene's bundles, then names their prefabs and counts their draws.
    /// </summary>
    private sealed partial class VegetationScan
    {
        /// <summary>
        /// The VegetationSpawner fields kept, written without their "m_".
        /// </summary>
        private static readonly string[] Keep = ["m_name", "m_enable", "m_min", "m_max", "m_forcePlacement", "m_scaleMin", "m_scaleMax", "m_randTilt", "m_chanceToUseGroundTilt", "m_biome", "m_biomeArea", "m_blockCheck", "m_snapToStaticSolid", "m_minAltitude", "m_maxAltitude", "m_minVegetation", "m_maxVegetation", "m_surroundCheckVegetation", "m_surroundCheckDistance", "m_surroundCheckLayers", "m_surroundBetterThanAverage", "m_minOceanDepth", "m_maxOceanDepth", "m_minTilt", "m_maxTilt", "m_terrainDeltaRadius", "m_maxTerrainDelta", "m_minTerrainDelta", "m_snapToWater", "m_groundOffset", "m_groupSizeMin", "m_groupSizeMax", "m_groupRadius", "m_minDistanceFromCenter", "m_maxDistanceFromCenter", "m_inForest", "m_forestTresholdMin", "m_forestTresholdMax"];

        /// <summary>
        /// Random draws made in Awake by these scripts (LodFadeInOut only with a LODGroup, Pickable only when it
        /// respawns: in <c>Finish</c>).
        /// </summary>
        private static readonly Dictionary<string, int> Draws = new() { ["LodFadeInOut"] = 1, ["MineRock5"] = 1, ["Floating"] = 1, ["LightFlicker"] = 1, ["Fish"] = 1, ["RandomFlyingBird"] = 2 };

        /// <summary>
        /// ZoneSystem's own rules with their prefabs, null until found.
        /// </summary>
        private List<(Dict Rule, Ref Prefab)>? _zone;
        /// <summary>
        /// The LocationLists, each once (some bundles repeat them): by (sort order, first rule's name, rule count).
        /// </summary>
        private readonly Dictionary<(long Order, string First, int Count), List<(Dict Rule, Ref Prefab)>> _lists = [];
        /// <summary>
        /// The alt biomes' added rules (each with "altBiome"), null until the alt biome list is found.
        /// </summary>
        private List<(Dict Rule, Ref Prefab)>? _alts;
        /// <summary>
        /// Whether each script has vegetation (by its file and script index), for the bundle being read.
        /// </summary>
        private readonly Dictionary<(Bundle.Inner, ushort), bool> _byScript = [];

        /// <summary>
        /// Whether ZoneSystem was found.
        /// </summary>
        public bool Found => _zone != null;

        /// <summary>
        /// Takes ZoneSystem's rules and each LocationList's from one bundle.
        /// </summary>
        public void Read(Bundle b)
        {
            // The scripts with vegetation, told by their fields in the type tree (once per script): only
            // their objects are read.
            _byScript.Clear();
            foreach (var f in b.Files)
            {
                foreach (var info in f.Assets.file.AssetInfos)
                {
                    if (info.TypeId != MonoBehaviourType || !HasVegetation(b, f, info) || b.Field(f, info.PathId) is not { } d)
                    {
                        continue;
                    }
                    TakeList(f, d);
                }
            }
        }

        /// <summary>
        /// Whether a MonoBehaviour's script has a vegetation list (its fields, read once per script).
        /// </summary>
        private bool HasVegetation(Bundle b, Bundle.Inner f, AssetFileInfo info)
        {
            ushort index = info.GetScriptIndex(f.Assets.file);
            if (!_byScript.TryGetValue((f, index), out bool has))
            {
                var fields = b.FieldNames(f, info);
                _byScript[(f, index)] = has = fields.Contains("m_vegetation") || fields.Contains("m_alts");
            }
            return has;
        }

        /// <summary>
        /// Takes the rules of a ZoneSystem (the first one found), of a LocationList (each sort order once) or of the
        /// alt biome list (the first one found).
        /// </summary>
        private void TakeList(Bundle.Inner f, AssetTypeValueField d)
        {
            if (Has(d, "m_alts"))
            {
                _alts ??= [.. d["m_alts.Array"].Children.SelectMany(a => Rules(f, a["m_addVegetation.Array"].Children).Select(r =>
                {
                    r.Rule["altBiome"] = a["m_name"].AsString;
                    return r;
                }))];
                return;
            }
            var vegs = d["m_vegetation.Array"].Children;
            if (Has(d, "m_zoneSize"))
            {
                _zone ??= Rules(f, vegs);
            }
            else if (Has(d, "m_sortOrder") && vegs.Count > 0)
            {
                _lists.TryAdd((Int(Py(d["m_sortOrder"])), vegs[0]["m_name"].AsString, vegs.Count), Rules(f, vegs));
            }
        }

        /// <summary>
        /// The rules of a vegetation list (fields of <c>Keep</c>) with their prefabs.
        /// </summary>
        private static List<(Dict Rule, Ref Prefab)> Rules(Bundle.Inner f, List<AssetTypeValueField> vegs) => [.. vegs.Select(v =>
        {
            var rule = new Dict();
            foreach (string k in Keep.Where(k => Has(v, k)))
            {
                rule[k[2..]] = Py(v[k]);
            }
            var p = v["m_prefab"];
            return (rule, new Ref(f.FileOf(p["m_FileID"].AsInt) ?? "", p["m_PathID"].AsLong));
        })];
    }
}
