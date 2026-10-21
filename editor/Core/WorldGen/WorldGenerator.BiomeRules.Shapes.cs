namespace ValheimGen;

// The shapes a world rule is checked against: its waved inner edge, its sector, its noise and its rectangle.
public sealed partial class WorldGenerator
{
    /// <summary>
    /// A rule's inner edge at an angle: its least distance waved (when it has one and the wave is on), or -0.1 for
    /// none, so that the middle itself passes.
    /// </summary>
    private static float InnerEdge(BiomeRule rule, float worldAngle)
    {
        float min = rule.MinDistance;
        if (min > 0f && rule.WiggleDistanceWidth > 0f)
        {
            return (float)((double)min + (double)Mathf.Sin((float)((double)worldAngle * rule.WiggleDistanceLength)) * rule.WiggleDistanceWidth);
        }
        return min == 0f ? -0.1f : min;
    }

    /// <summary>
    /// Whether a point's angle (a fraction of the circle, waved along the radius) falls in a rule's sector; every
    /// point does for a rule without one. A sector past 1 wraps round.
    /// </summary>
    private static bool InSector(BiomeRule rule, float baseAngle, float magnitude)
    {
        float min = rule.MinSector, max = rule.MaxSector;
        if (min == 0f && max == 1f)
        {
            return true;
        }
        float angle = baseAngle;
        if (rule.WiggleSectorWidth > 0f)
        {
            angle = (float)((double)angle + (double)Mathf.Sin((float)((double)magnitude / rule.WiggleSectorLength)) * rule.WiggleSectorWidth);
        }
        if (angle < 0f)
        {
            angle += 1f;
        }
        if (angle >= 1f)
        {
            angle -= 1f;
        }
        return min > max ? angle >= min || angle < max : angle >= min && angle < max;
    }

    /// <summary>
    /// Whether a point's noise field (the rule's seed, the biome-stretched point over the rule's own stretch) is high
    /// enough for the rule's amount: above 1 minus the amount (at it too in the legacy evaluation).
    /// </summary>
    private bool PassesNoise(BiomeRule rule, float bx, float by, bool legacy)
    {
        float seed = rule.Seed ?? SeedOf(rule.SeedBiome);
        float noise = GenMath.Noise((float)(((double)seed + (double)bx / rule.Stretch) * (double)0.001f), (float)(((double)seed + (double)by / rule.Stretch) * (double)0.001f));
        double limit = 1.0 - rule.Amount;
        return legacy ? noise >= limit : noise > limit;
    }

    /// <summary>
    /// How far inside a rule's (waved) rectangle a point lies, metres; negative outside. The point is in world metres.
    /// </summary>
    private static float RectangleEdge(BiomeRule rule, float sx, float sy)
    {
        float dx = (float)((double)sx - rule.CenterX), dy = (float)((double)sy - rule.CenterY);
        float lx = (float)((double)dx * rule.RotationCos - (double)dy * rule.RotationSin);
        float ly = (float)((double)dx * rule.RotationSin + (double)dy * rule.RotationCos);
        float limitX = rule.HalfSizeX, limitY = rule.HalfSizeY;
        if (rule.WiggleRectangleWidth > 0f)
        {
            limitX = (float)((double)limitX + (double)Mathf.Sin((float)((double)ly / rule.WiggleRectangleLength)) * rule.WiggleRectangleWidth);
            limitY = (float)((double)limitY + (double)Mathf.Sin((float)((double)lx / rule.WiggleRectangleLength)) * rule.WiggleRectangleWidth);
        }
        return Mathf.Min((float)((double)limitX - Mathf.Abs(lx)), (float)((double)limitY - Mathf.Abs(ly)));
    }
}
