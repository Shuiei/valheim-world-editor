using ValheimGen;

namespace TerrainEditor.App;

public static partial class SeedPreview
{
    /// <summary>How well a preview meets the wishes, from 0 up (higher is better).</summary>
    public static float Score(Stats s, Wishes w)
    {
        float score = 0;
        if (w.BigStartLand)
        {
            score += 3 * s.StartContinent;
        }
        if (w.LandAroundStart)
        {
            score += 2 * s.LandNearStart;
        }
        static float Within(float d, float max) => max <= 0 ? 0 : float.IsPositiveInfinity(d) ? -2 : d <= max ? 1 + (max - d) / max : -(d - max) / max;
        float Near(Heightmap.Biome b, float max) => Within(s.Distance[b], max);
        score += Near(Heightmap.Biome.Swamp, w.MaxSwampDistance) + Near(Heightmap.Biome.Mountain, w.MaxMountainDistance) + Near(Heightmap.Biome.Plains, w.MaxPlainsDistance);
        if (s.Nearest is { } n)
        {
            float To(string name) => n.TryGetValue(name, out float d) ? d : float.PositiveInfinity;
            score += Within(To("Haldor"), w.MaxHaldorDistance) + Within(To("The Bog Witch"), w.MaxBogWitchDistance) + Within(To("Hildir"), w.MaxHildirDistance);
            if (w.MaxBossDistance > 0)
            {
                score += EarlyBosses.Average(b => Within(To(b), w.MaxBossDistance));
            }
        }
        if (w.BiomesOnStartLand)
        {
            score += 0.25f * s.SameLand.Count(kv => kv.Key is not (Heightmap.Biome.AshLands or Heightmap.Biome.DeepNorth) && kv.Value);
        }
        if (s.Land < w.MinLand)
        {
            score -= 5 * (w.MinLand - s.Land);
        }
        if (s.Land > w.MaxLand)
        {
            score -= 5 * (s.Land - w.MaxLand);
        }
        return MathF.Max(0, score);
    }
}
