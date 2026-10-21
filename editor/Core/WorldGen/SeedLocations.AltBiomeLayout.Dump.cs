using System.Globalization;
using Rnd = ValheimGen.UnityEngine.Random;

namespace ValheimGen;

public static partial class SeedLocations
{
    internal sealed partial class AltBiomeLayout
    {
        /// <summary>
        /// A seed's areas and alt biomes as lines to set against the game's (the AltDump test plugin's format: each
        /// alt biome with its count, then each area in the order made with its biome, edge, middle, heights and
        /// distance as float bits, its neighbours and its alt biomes).
        /// </summary>
        /// <param name="map">The seed's biome point map.</param>
        /// <param name="gen">The seed's generator.</param>
        /// <param name="alts">The alt biomes, in the game's order.</param>
        internal static List<string> DumpLines(BiomeMap map, WorldGenerator gen, IReadOnlyList<AltBiomeRule> alts)
        {
            var areas = new Areas(map, gen.Rules.Biomes, gen.Rules.DataInstalled, CancellationToken.None);
            Rnd.RandomState saved = Rnd.State;
            Dictionary<string, int> given;
            try
            {
                given = Place(areas, gen, alts);
            }
            finally
            {
                Rnd.State = saved;
            }
            List<string> lines = [$"# seed {gen.GetSeed()} size {map.Size} sectors {areas.Sectors.Count}"];
            lines.AddRange(alts.Select(a => $"A {a.Name}|{given.GetValueOrDefault(a.Name)}"));
            for (int i = 0; i < areas.Sectors.Count; i++)
            {
                var s = areas.Sectors[i];
                lines.Add(string.Create(CultureInfo.InvariantCulture,
                    $"S {i} {(int)s.Biome} {s.EdgeCount} {Bits(s.CenterX)} {Bits(s.CenterY)} {Bits(s.HeightMin)} {Bits(s.HeightMax)} {Bits(s.HeightAvg)} {Bits(s.DistanceFromCenter)} n={string.Join(",", s.Neighbors)} a={string.Join("|", s.Alts.Select(a => a.Name))}"));
            }
            return lines;
        }

        /// <summary>A float's bits in hex.</summary>
        private static string Bits(float f) => BitConverter.SingleToInt32Bits(f).ToString("x8", CultureInfo.InvariantCulture);
    }
}
