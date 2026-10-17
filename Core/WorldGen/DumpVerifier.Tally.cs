namespace ValheimGen;

public static partial class DumpVerifier
{
    /// <summary>
    /// The counts, the largest differences and the first mismatches of one replay, and the generator it builds on.
    /// </summary>
    private sealed class Tally
    {
        /// <summary>Random calls that matched.</summary>
        public int RandomOk { get; set; }
        /// <summary>Random calls that did not.</summary>
        public int RandomWrong { get; set; }
        /// <summary>Perlin samples that matched.</summary>
        public int PerlinOk { get; set; }
        /// <summary>Perlin samples that did not.</summary>
        public int PerlinWrong { get; set; }
        /// <summary>Point heights that matched.</summary>
        public int HeightsOk { get; set; }
        /// <summary>Point heights that did not.</summary>
        public int HeightsWrong { get; set; }
        /// <summary>Points whose biome did not match.</summary>
        public int BiomesWrong { get; set; }
        /// <summary>Zones whose every base height matched.</summary>
        public int ZonesOk { get; set; }
        /// <summary>Zones with any height that did not.</summary>
        public int ZonesWrong { get; set; }
        /// <summary>Lake, river and stream count lines that did not match.</summary>
        public int WaterWrong { get; set; }
        /// <summary>Lines that could not be read.</summary>
        public int Unreadable { get; set; }
        /// <summary>The largest Perlin difference.</summary>
        public double PerlinMax { get; set; }
        /// <summary>The largest point height difference, metres.</summary>
        public double HeightMax { get; set; }
        /// <summary>The largest height difference inside zones, metres.</summary>
        public double ZoneMax { get; set; }
        /// <summary>The first mismatches (at most 25), described.</summary>
        public List<string> Errors { get; } = [];
        /// <summary>The generator of the seed, built when a line first needs it.</summary>
        public WorldGenerator? Generator { get; set; }

        /// <summary>
        /// Keeps a mismatch's description while fewer than 25 are kept.
        /// </summary>
        /// <param name="msg">What did not match.</param>
        public void Error(string msg)
        {
            if (Errors.Count < 25)
            {
                Errors.Add(msg);
            }
        }
    }
}
