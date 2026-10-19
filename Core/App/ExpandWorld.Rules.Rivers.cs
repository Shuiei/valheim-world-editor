using ValheimGen;

namespace TerrainEditor.App;

// Expand World Rivers' settings (expand_world_rivers.cfg): the water level, the lakes, the rivers and the streams.
public static partial class ExpandWorld
{
    /// <summary>The rules with Expand World Rivers' settings from its settings file (a setting absent: the game's own value).</summary>
    /// <param name="rules">The rules so far.</param>
    /// <param name="rivers">The settings file's values by name.</param>
    private static WorldRules WithRivers(WorldRules rules, IReadOnlyDictionary<string, string> rivers)
    {
        var v = WorldRules.Vanilla;
        string? R(string name) => rivers.GetValueOrDefault(name);
        return rules with
        {
            WaterLevel = Float(R("Water level"), v.WaterLevel),
            LakeSearchInterval = Float(R("Lake search interval"), v.LakeSearchInterval),
            LakeDepth = Float(R("Lake depth"), v.LakeDepth),
            LakeMergeRadius = Float(R("Lake merge radius"), v.LakeMergeRadius),
            LakeMaxDistance1 = Float(R("Lake max distance 1"), v.LakeMaxDistance1),
            LakeMaxDistance2 = Float(R("Lake max distance 2"), v.LakeMaxDistance2),
            Rivers = Bool(R("Rivers"), v.Rivers),
            RiverCheckInterval = Float(R("River check interval"), v.RiverCheckInterval),
            RiverMaxAltitude = Float(R("River max altitude"), v.RiverMaxAltitude),
            RiverMinWidth = Float(R("River min width"), v.RiverMinWidth),
            RiverMaxWidth = Float(R("River max width"), v.RiverMaxWidth),
            RiverCurveWidth = Float(R("River curve width"), v.RiverCurveWidth),
            RiverCurveWaveLength = Float(R("River curve wave length"), v.RiverCurveWaveLength),
            RiverSeed = Int(R("River seed")),
            Streams = Bool(R("Streams"), v.Streams),
            StreamSeed = Int(R("Stream seed")),
            StreamMaxAmount = Int(R("Stream max amount"), v.StreamMaxAmount),
            StreamSearchIterations = Int(R("Stream search iterations"), v.StreamSearchIterations),
            StreamStartMinAltitude = Float(R("Stream start min altitude"), v.StreamStartMinAltitude),
            StreamStartMaxAltitude = Float(R("Stream start max altitude"), v.StreamStartMaxAltitude),
            StreamEndMinAltitude = Float(R("Stream end min altitude"), v.StreamEndMinAltitude),
            StreamEndMaxAltitude = Float(R("Stream end max altitude"), v.StreamEndMaxAltitude),
            StreamMaxWidth = Float(R("Stream max width"), v.StreamMaxWidth),
            StreamMinLength = Float(R("Stream min length"), v.StreamMinLength),
            StreamMaxLength = Float(R("Stream max length"), v.StreamMaxLength),
            StreamCurveWidth = Float(R("Stream curve width"), v.StreamCurveWidth),
            StreamCurveWaveLength = Float(R("Stream curve wave length"), v.StreamCurveWaveLength),
        };
    }
}
