namespace ValheimGen;

public sealed partial class WorldGenerator
{
    // The seed's draws the rules replace (Expand World Size's offsets and height seed, Expand World Rivers' seeds).
    private sealed partial class RuleValues
    {
        /// <summary>The seed's draws, or the rules' values in their place.</summary>
        private void SetSeeds(WorldRules r, float offsetA, float offsetB, float offsetDetail, int riverSeed, int streamSeed)
        {
            ContinentA = r.OffsetX ?? offsetA;
            ContinentB = r.OffsetY ?? offsetB;
            DetailX = r.HeightSeed ?? offsetDetail;
            RiverSeed = r.RiverSeed ?? riverSeed;
            StreamSeed = r.StreamSeed ?? streamSeed;
        }

        /// <summary>
        /// The continents' first offset as the continents' height reads it: the seed's, or the rules' Offset X (which
        /// replaces it there, and only there: the swamps' field keeps the seed's).
        /// </summary>
        public float ContinentA { get; private set; }
        /// <summary>The continents' second offset as the continents' height reads it: the seed's, or the rules' Offset Y.</summary>
        public float ContinentB { get; private set; }
        /// <summary>
        /// The detail offset as the biomes' heights read it on their x coordinate: the seed's, or the rules' height
        /// seed (which replaces the first use in each of the game's seven biome heights, the x coordinate's; z keeps
        /// the seed's).
        /// </summary>
        public float DetailX { get; private set; }
        /// <summary>The rivers' seed: the seed's, or the rules'.</summary>
        public int RiverSeed { get; private set; }
        /// <summary>The streams' seed: the seed's, or the rules'.</summary>
        public int StreamSeed { get; private set; }
    }
}
