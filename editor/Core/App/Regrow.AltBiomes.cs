using ValheimGen;
using Rule = ValheimGen.VegetationRule;

namespace TerrainEditor.App;

// The alt biomes' say over vegetation (PlaceVegetation's check of each try).
public static partial class Regrow
{
    /// <summary>
    /// Whether a world's alt biomes let a rule's group stand at a point: an alt biome's rule only in that alt biome's
    /// areas, no rule an alt biome of the area blocks. The alt biomes are laid out (once a world) the first time a rule
    /// that depends on them asks.
    /// </summary>
    /// <param name="terrain">The world's terrain (its generator).</param>
    private sealed class AltCheck(TerrainService terrain)
    {
        /// <summary>The names of the rules an alt biome blocks.</summary>
        private readonly HashSet<string> _blocked = [.. SeedLocations.AltBiomeRulesFor(terrain.Generator).SelectMany(a => a.BlockVegetationNames)];

        /// <summary>The world's alt biome layout, null until asked for.</summary>
        private SeedLocations.AltBiomeLayout? _layout;

        /// <summary>Whether a group of the rule may stand at a point.</summary>
        public bool Lets(Rule veg, float x, float z)
        {
            if (veg.altBiome == null && !_blocked.Contains(veg.name))
            {
                return true;
            }
            _layout ??= SeedLocations.AltBiomesOf(terrain.Generator);
            return _layout.LetsVegetation(veg.altBiome, veg.name, x, z);
        }
    }
}
