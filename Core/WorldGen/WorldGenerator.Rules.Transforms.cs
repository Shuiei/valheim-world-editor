namespace ValheimGen;

public sealed partial class WorldGenerator
{
    // The transforms the rules lay over the game's heights and forest, each applied only when its setting is set, as
    // the mod applies its postfix only then (at the default the formula would round differently from the game).
    private sealed partial class RuleValues
    {
        /// <summary>
        /// The continents' height with the rules' altitude transform (Expand World Size's postfix on the game's
        /// GetBaseHeight, applied only when its delta or multiplier is set).
        /// </summary>
        public float Altitude(float h) => AltitudeOn ? BaseWater + (h - BaseWater) * AltitudeMultiplier + BaseAltitudeDelta : h;

        /// <summary>
        /// A biome's height with the rules' water depth multiplier (Expand World Size's postfix on the game's
        /// GetBiomeHeight, applied only when set): the ground under the water level, deeper or shallower.
        /// </summary>
        public float Depth(float h) => WaterDepthOn && !(h > WaterLevel) ? (h - WaterLevel) * WaterDepth + WaterLevel : h;

        /// <summary>The forest factor divided by the rules' forest multiplier (applied only when set).</summary>
        public float Forest(float factor) => ForestOn ? factor / ForestMultiplier : factor;
    }
}
