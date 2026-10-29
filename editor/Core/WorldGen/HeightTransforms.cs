namespace ValheimGen;

/// <summary>A biome's or territory's height transform (<see cref="HeightTransform"/>) as the generator applies it.</summary>
public static class HeightTransforms
{
    /// <summary>
    /// An altitude (metres from the water level) transformed, as the mod's ApplyHeight computes it (floats, each step
    /// rounded as its store rounds it).
    /// </summary>
    /// <param name="t">The transform.</param>
    /// <param name="altitude">The altitude, metres from the water level.</param>
    public static float Apply(this HeightTransform t, float altitude)
    {
        float factor = Mathf.Abs(t.ExcessFactor), sign = Mathf.Sign(t.ExcessFactor);
        float h = (float)((double)altitude * t.Multiplier);
        h = (float)((double)h + t.Delta);
        if (h < 0f)
        {
            h = (float)((double)h * t.DepthMultiplier);
        }
        if (h > t.Maximum)
        {
            h = (float)((double)t.Maximum + (double)sign * Mathf.Pow((float)((double)h - t.Maximum), factor));
        }
        if (h < t.Minimum)
        {
            h = (float)((double)t.Minimum - (double)sign * Mathf.Pow((float)((double)t.Minimum - h), factor));
        }
        return h;
    }
}
