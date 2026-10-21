namespace ValheimGen;

public sealed partial class WorldGenerator
{
    // The rules' values for the water: the lakes, rivers and streams (Expand World Rivers).
    private sealed partial class RuleValues
    {
        /// <summary>The water's values from the rules.</summary>
        private void SetWater(WorldRules r)
        {
            LakeStep = r.LakeSearchInterval;
            LakeDepth = r.AltitudeToBaseHeight(r.LakeDepth);
            LakeMerge = r.LakeMergeRadius;
            RiversOn = r.Rivers;
            RiverReach1 = r.LakeMaxDistance1;
            RiverReach2 = r.LakeMaxDistance2;
            RiverLimit = r.AltitudeToBaseHeight(r.RiverMaxAltitude);
            RiverStep = r.RiverCheckInterval;
            RiverWidthMin = r.RiverMinWidth;
            RiverWidthMax = r.RiverMaxWidth;
            RiverCurveWidth = r.RiverCurveWidth;
            RiverCurveWaveLength = r.RiverCurveWaveLength;
            StreamsOn = r.Streams;
            StreamTries = r.StreamMaxAmount;
            StreamIterations = r.StreamSearchIterations;
            StreamStartMin = r.AltitudeToHeight(r.StreamStartMinAltitude);
            StreamStartMax = r.AltitudeToHeight(r.StreamStartMaxAltitude);
            StreamEndMin = r.AltitudeToHeight(r.StreamEndMinAltitude);
            StreamEndMax = r.AltitudeToHeight(r.StreamEndMaxAltitude);
            StreamMinLength = r.StreamMinLength;
            StreamMaxLength = r.StreamMaxLength;
            StreamWidthMax = r.StreamMaxWidth;
            StreamCurveWidth = r.StreamCurveWidth;
            StreamCurveWaveLength = r.StreamCurveWaveLength;
        }

        /// <summary>How far apart the lake scan's points are (128.0).</summary>
        public double LakeStep { get; private set; }
        /// <summary>The continents' height under which a point is a lake (0.05f).</summary>
        public float LakeDepth { get; private set; }
        /// <summary>Lake points within this distance merge (800f).</summary>
        public float LakeMerge { get; private set; }
        /// <summary>Whether rivers are laid out.</summary>
        public bool RiversOn { get; private set; }
        /// <summary>How far a lake looks for a river's other end (2000f).</summary>
        public float RiverReach1 { get; private set; }
        /// <summary>How far a lake without a river looks (5000f).</summary>
        public float RiverReach2 { get; private set; }
        /// <summary>The continents' height a river never crosses (0.4f).</summary>
        public float RiverLimit { get; private set; }
        /// <summary>How far apart a river's way is checked (128f).</summary>
        public float RiverStep { get; private set; }
        /// <summary>A river's narrowest width draw's floor (60f).</summary>
        public float RiverWidthMin { get; private set; }
        /// <summary>A river's widest width draw's ceiling (100f).</summary>
        public float RiverWidthMax { get; private set; }
        /// <summary>A river's waves' width, as a share of its length (15.0).</summary>
        public double RiverCurveWidth { get; private set; }
        /// <summary>A river's waves' length, as a share of its length (20.0).</summary>
        public double RiverCurveWaveLength { get; private set; }
        /// <summary>Whether streams are laid out.</summary>
        public bool StreamsOn { get; private set; }
        /// <summary>How many streams are tried (3000).</summary>
        public int StreamTries { get; private set; }
        /// <summary>How many points are tried for a stream's start, and for its end (100).</summary>
        public int StreamIterations { get; private set; }
        /// <summary>A stream's start is above this height, metres (26f).</summary>
        public float StreamStartMin { get; private set; }
        /// <summary>A stream's start is below this height (31f).</summary>
        public float StreamStartMax { get; private set; }
        /// <summary>A stream's end is above this height (36f).</summary>
        public float StreamEndMin { get; private set; }
        /// <summary>A stream's end is below this height (44f).</summary>
        public float StreamEndMax { get; private set; }
        /// <summary>The shortest a stream is tried at (80f).</summary>
        public float StreamMinLength { get; private set; }
        /// <summary>The longest a stream is tried at (200f).</summary>
        public float StreamMaxLength { get; private set; }
        /// <summary>A stream's widest point (20f; the mod replaces this literal only, the narrowest stays 20).</summary>
        public float StreamWidthMax { get; private set; }
        /// <summary>A stream's waves' width, as a share of its length (15.0).</summary>
        public double StreamCurveWidth { get; private set; }
        /// <summary>A stream's waves' length, as a share of its length (20.0).</summary>
        public double StreamCurveWaveLength { get; private set; }
    }
}
