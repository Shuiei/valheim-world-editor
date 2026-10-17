using TerrainEditor.App;

namespace WorldEditor.Tests;

/// <summary>
/// Builds the private Regrow types that the placement steps take (a vegetation rule, the ground, a zone's heightmap)
/// with the values the tests need. A rule from <see cref="OpenRule"/> passes every check; a test changes one value to
/// make one check fail, so each check is tested alone.
/// </summary>
internal static class RegrowFixtures
{
    private static Type RuleType { get; } = PrivateMember.Nested(typeof(Regrow), "Rule");
    private static Type GroundType { get; } = PrivateMember.Nested(typeof(Regrow), "Ground");
    private static Type ZoneType { get; } = PrivateMember.Nested(typeof(Regrow), "Zone");

    /// <summary>The private rule type, for the tests that name it.</summary>
    public static Type Rule => RuleType;

    /// <summary>The private zone type, for the tests that build dictionaries of zones.</summary>
    public static Type Zone => ZoneType;

    /// <summary>
    /// A rule that passes every check at any spot: every biome and area, no altitude, mask, ocean, slope or distance
    /// limit, one object per group, placed once.
    /// </summary>
    /// <param name="prefab">The prefab placed.</param>
    public static object OpenRule(string prefab = "Pine")
    {
        object rule = PrivateMember.New(RuleType);
        PrivateMember.Set(rule, "prefab", prefab);
        PrivateMember.Set(rule, "enable", 1);
        PrivateMember.Set(rule, "biome", -1);
        PrivateMember.Set(rule, "min", 1f);
        PrivateMember.Set(rule, "max", 1f);
        PrivateMember.Set(rule, "groupSizeMin", 1);
        PrivateMember.Set(rule, "groupSizeMax", 1);
        return rule;
    }

    /// <summary>A flat ground of 81 × 81 metres at <paramref name="height"/>, from (-40, -40) to (40, 40).</summary>
    public static object FlatGround(float height = 0f)
    {
        const int size = 81;
        float[] heights = new float[size * size];
        Array.Fill(heights, height);
        return PrivateMember.New(GroundType, heights, size, size, -40f, -40f);
    }

    /// <summary>
    /// A ground of 20 × 20 metres from (-10, -10) to (10, 10) whose height rises by <paramref name="slope"/> per metre
    /// along x (0: flat).
    /// </summary>
    public static object SlopedGround(float slope)
    {
        const int size = 20;
        float[] heights = new float[size * size];
        for (int z = 0; z < size; z++)
        {
            for (int x = 0; x < size; x++)
            {
                heights[z * size + x] = x * slope;
            }
        }
        return PrivateMember.New(GroundType, heights, size, size, -10f, -10f);
    }

    /// <summary>
    /// A zone (at the world's origin) of one biome in all four corners, with a zero paint mask and no ocean: its
    /// biome is <paramref name="biome"/> everywhere and its area is the median.
    /// </summary>
    public static object FlatZone(int biome = 4)
    {
        object zone = PrivateMember.New(ZoneType);
        PrivateMember.Set(zone, "Corners", new[] { biome, biome, biome, biome });
        PrivateMember.Set(zone, "Mask", new float[65 * 65 * 4]);
        PrivateMember.Set(zone, "OceanDepth", new float[4]);
        PrivateMember.Set(zone, "Cx", 0f);
        PrivateMember.Set(zone, "Cz", 0f);
        return zone;
    }
}
