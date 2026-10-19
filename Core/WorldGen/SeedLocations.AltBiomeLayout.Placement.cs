using TerrainEditor.Save;
using Rnd = ValheimGen.UnityEngine.Random;

namespace ValheimGen;

public static partial class SeedLocations
{
    internal sealed partial class AltBiomeLayout
    {
        /// <summary>
        /// Gives the alt biomes to the areas (GenerateAltBiomes): biome by biome in the game's order, each alt biome
        /// of that biome (in the list's order) shuffles the biome's areas from the seed and its own name, then goes
        /// through them: while it has fewer than its most, one draw per area, and the area is given it when it still
        /// lacks its least or the draw is within its chance, and the area may have it. An alt biome's count is kept
        /// over all its biomes. Expand World Data's forced areas take part in the draws but are given nothing then;
        /// their alt biomes are added after.
        /// </summary>
        /// <returns>How many areas each alt biome got, forced ones included.</returns>
        private static Dictionary<string, int> Place(Areas areas, WorldGenerator gen, IReadOnlyList<AltBiomeRule> alts)
        {
            var forced = Forced(areas, gen, alts);
            // Expand World Size stretches the alt biomes' distances with the world.
            float stretch = gen.Rules.Stretch;
            int[] counts = new int[alts.Count];
            foreach (var biome in areas.Order)
            {
                if (!areas.ByBiome.TryGetValue(biome, out var sectors))
                {
                    continue;
                }
                for (int a = 0; a < alts.Count; a++)
                {
                    var alt = alts[a];
                    if (!alt.Enabled || (alt.Biome & (int)biome) != (int)biome)
                    {
                        continue;
                    }
                    Rnd.InitState(unchecked((int)biome + StableHash.Of(alt.Name) + gen.GetSeed()));
                    Shuffle(sectors);
                    foreach (int s in sectors)
                    {
                        if (counts[a] >= alt.MaxAmountSpawned)
                        {
                            continue;
                        }
                        float draw = Rnd.Range(0f, 1f);
                        var sector = areas.Sectors[s];
                        if ((counts[a] < alt.MinAmountSpawned || alt.Chance >= draw) && !forced.ContainsKey(s) && MayHave(areas, sector, alt, stretch))
                        {
                            counts[a]++;
                            sector.Alts.Add(alt);
                        }
                    }
                }
            }
            foreach (var (s, list) in forced)
            {
                areas.Sectors[s].Alts.AddRange(list);
            }
            var given = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var sector in areas.Sectors)
            {
                foreach (var alt in sector.Alts)
                {
                    given[alt.Name] = given.GetValueOrDefault(alt.Name) + 1;
                }
            }
            return given;
        }

        /// <summary>
        /// Utils.Shuffle: from the last item down, each swapped with one drawn among it and those before it.
        /// </summary>
        private static void Shuffle(List<int> list)
        {
            for (int n = list.Count - 1; n > 0; n--)
            {
                int k = Rnd.Range(0, n + 1);
                (list[n], list[k]) = (list[k], list[n]);
            }
        }

        /// <summary>
        /// The areas Expand World Data's world rules force alt biomes onto (its prefix of GenerateAltBiomes): each
        /// area, in the game's order, whose middle passes a rule naming alt biomes gets those of them the list has
        /// (names in any case; others are left out). None without such rules.
        /// </summary>
        private static Dictionary<int, List<AltBiomeRule>> Forced(Areas areas, WorldGenerator gen, IReadOnlyList<AltBiomeRule> alts)
        {
            Dictionary<int, List<AltBiomeRule>> forced = [];
            if (!gen.ForcesAltBiomes)
            {
                return forced;
            }
            var byName = new Dictionary<string, AltBiomeRule>(StringComparer.OrdinalIgnoreCase);
            foreach (var alt in alts)
            {
                byName[alt.Name] = alt;
            }
            foreach (var biome in areas.Order)
            {
                foreach (int s in areas.ByBiome.GetValueOrDefault(biome) ?? [])
                {
                    var sector = areas.Sectors[s];
                    if (gen.ForcedAltBiomesAt(sector.CenterX, sector.CenterY) is not { } names)
                    {
                        continue;
                    }
                    List<AltBiomeRule> list = [.. names.Select(n => byName.GetValueOrDefault(n)).OfType<AltBiomeRule>()];
                    if (list.Count > 0)
                    {
                        forced[s] = list;
                    }
                }
            }
            return forced;
        }

        /// <summary>
        /// Whether an area may have an alt biome (BiomeSector.CanAddModifier): far enough from the world's middle, an
        /// edge neither too short nor too long, an average height in range, a middle within the alt biome's bounds
        /// (each only when not 0), no alt biome either side calls incompatible, and the neighbours it asks for or
        /// forbids.
        /// </summary>
        private static bool MayHave(Areas areas, Sector sector, AltBiomeRule alt, float stretch) =>
            Measured(sector, alt, stretch) && WithinBounds(sector, alt, stretch) && Compatible(sector, alt)
            && Neighbours(areas, sector, alt.RequireNeighbor, required: true) && Neighbours(areas, sector, alt.NotNeighbor, required: false);

        /// <summary>
        /// Whether an area is far enough from the world's middle (the distance stretched as Expand World Size
        /// stretches it), its edge neither too short nor too long, and its edge's average height in range.
        /// </summary>
        private static bool Measured(Sector sector, AltBiomeRule alt, float stretch) =>
            sector.DistanceFromCenter >= (float)((double)alt.MinDistanceFromCenter * stretch)
            && sector.EdgeCount >= alt.MinEdgeSize && sector.EdgeCount < alt.MaxEdgeSize
            && sector.HeightAvg >= alt.MinAvgHeight && sector.HeightAvg < alt.MaxAvgHeight;

        /// <summary>
        /// Whether an area's middle is within the alt biome's bounds (each only when not 0; stretched as Expand World
        /// Size stretches them).
        /// </summary>
        private static bool WithinBounds(Sector sector, AltBiomeRule alt, float stretch)
        {
            static float Stretched(float bound, float stretch) => (float)((double)bound * stretch);
            float aboveX = Stretched(alt.AboveWorldX, stretch), belowX = Stretched(alt.BelowWorldX, stretch);
            float aboveY = Stretched(alt.AboveWorldY, stretch), belowY = Stretched(alt.BelowWorldY, stretch);
            return !((aboveX != 0f && sector.CenterX < aboveX) || (belowX != 0f && sector.CenterX > belowX)
                || (aboveY != 0f && sector.CenterY < aboveY) || (belowY != 0f && sector.CenterY > belowY));
        }

        /// <summary>
        /// Whether no alt biome the area has calls this one incompatible, and this one calls none of them so.
        /// </summary>
        private static bool Compatible(Sector sector, AltBiomeRule alt) =>
            !sector.Alts.Any(a => a.IncompatibleAltBiomes.Contains(alt.Name)) && !alt.IncompatibleAltBiomes.Any(n => sector.Alts.Any(a => a.Name == n));

        /// <summary>
        /// The neighbour test as the game makes it: for each of the first ten biome indices whose biome the flags hold
        /// (index 0 is none, which every set holds), a neighbour is looked for whose biome is the index itself taken
        /// as a biome value (1 Meadows, 2 Swamp, 4 Mountain, 8 Black Forest; the other indices match nothing). So an
        /// alt biome that asks for any neighbour is never given (no area is of biome 0), and of the neighbours one
        /// forbids, only these count: Meadows, Swamp, Mountain (when it forbids the Black Forest) and the Black Forest
        /// (when it forbids the ocean).
        /// </summary>
        /// <param name="areas">The areas.</param>
        /// <param name="sector">The area.</param>
        /// <param name="flags">The biomes asked for or forbidden; none: no test.</param>
        /// <param name="required">Asked for (each must be met) rather than forbidden (none may be).</param>
        private static bool Neighbours(Areas areas, Sector sector, int flags, bool required)
        {
            if (flags == 0)
            {
                return true;
            }
            for (int i = 0; i < 10; i++)
            {
                int bit = (int)BiomeTable.AtIndex(i, dataInstalled: false);
                if ((flags & bit) != bit)
                {
                    continue;
                }
                bool met = sector.Neighbors.Any(n => (int)areas.Sectors[n].Biome == i);
                if (met != required)
                {
                    return false;
                }
            }
            return true;
        }
    }
}
