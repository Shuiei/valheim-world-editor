namespace ValheimGen;

public sealed partial class WorldGenerator
{
    // The rules' values for the biomes: Expand World Data's biome table, world rules and territories.
    private sealed partial class RuleValues
    {
        /// <summary>The biomes' values from the rules.</summary>
        private void SetBiomes(WorldRules r)
        {
            WiggleFrequency = r.WiggleFrequency;
            TotalRadius = r.TotalRadius;
            BiomeStretchF = r.BiomeStretch;
            Biomes = r.Biomes;
            BiomeData = r.BiomeData;
            World = r.World;
            Legacy = r.LegacyGeneration;
            CheckSectors = r.CheckSectors;
            // The mod keeps three lists of the same rules: those naming a biome, a territory, alt biomes.
            BiomeRules = r.World?.Where(rule => rule.Biome != Heightmap.Biome.None).ToList() ?? [];
            TerritoryRules = r.World?.Where(rule => rule.Territory != "").ToList() ?? [];
            Dictionary<string, Territory> territories = [];
            foreach (var t in r.Territories)
            {
                territories[t.Name] = t;
            }
            Territories = territories;
            AnyBiomeData = BiomeData || territories.Count > 0;
            UniformBiomeDraw = r.DataInstalled;
        }

        /// <summary>How many waves the edge circles and the rules' distance limits make round the world (20f).</summary>
        public float WiggleFrequency { get; private set; }
        /// <summary>The edge of the world in world metres, past which no rule applies (10500f).</summary>
        public float TotalRadius { get; private set; }
        /// <summary>The land biomes' noise fields' stretch as the rules divide a point by it (a float, as the mod's WorldInfo keeps it).</summary>
        public float BiomeStretchF { get; private set; }
        /// <summary>The world's biomes.</summary>
        public BiomeTable Biomes { get; private set; } = BiomeTable.Vanilla;
        /// <summary>Whether the biome file is in use (its terrain prefix and height postfix apply).</summary>
        public bool BiomeData { get; private set; }
        /// <summary>The world rules, or null for the game's own biome layout.</summary>
        public IReadOnlyList<BiomeRule>? World { get; private set; }
        /// <summary>The world rules that name a biome (what a point's biome and the sea's heat are read from).</summary>
        public List<BiomeRule> BiomeRules { get; private set; } = [];
        /// <summary>Whether the rules are evaluated the older way.</summary>
        public bool Legacy { get; private set; }
        /// <summary>Whether any rule limits its sector.</summary>
        public bool CheckSectors { get; private set; }
        /// <summary>The world rules that name a territory (none without world rules).</summary>
        public List<BiomeRule> TerritoryRules { get; private set; } = [];
        /// <summary>The territories by name.</summary>
        public IReadOnlyDictionary<string, Territory> Territories { get; private set; } = new Dictionary<string, Territory>();
        /// <summary>Whether the biome file or the territory file is in use: a height passes the mod's postfix.</summary>
        public bool AnyBiomeData { get; private set; }
        /// <summary>Whether a point is drawn from a set of biomes uniformly (Expand World Data installed) instead of by the game's walk.</summary>
        public bool UniformBiomeDraw { get; private set; }
    }

    /// <summary>
    /// The seed of a biome's noise field in the world rules, as the mod keeps them: the seed's draws for the game's
    /// five land biomes (the swamps' A, the plains' B, the forests', the detail's for the Meadows, the mists'), the four
    /// extra draws for the Ashlands, the Deep North, the mountains and the ocean, and the swamps' for any other.
    /// </summary>
    /// <param name="biome">The biome.</param>
    private float SeedOf(Heightmap.Biome biome) => biome switch
    {
        Heightmap.Biome.Swamp => _offsetA,
        Heightmap.Biome.Plains => _offsetB,
        Heightmap.Biome.BlackForest => _offsetForest,
        Heightmap.Biome.Meadows => _offsetDetail,
        Heightmap.Biome.Mistlands => _offsetMist,
        Heightmap.Biome.AshLands => _offsetAsh,
        Heightmap.Biome.DeepNorth => _offsetNorth,
        Heightmap.Biome.Mountain => _offsetMountain,
        Heightmap.Biome.Ocean => _offsetOcean,
        _ => _offsetA,
    };

    /// <summary>
    /// The four draws Expand World Data makes before the game lays out its water (its Pregenerate prefix, after the
    /// seed's seven): the noise seeds of the Ashlands, the Deep North, the mountains and the ocean in its world rules.
    /// Drawn only when the mod is installed; 0 otherwise.
    /// </summary>
    private readonly float _offsetAsh, _offsetNorth, _offsetMountain, _offsetOcean;
}
