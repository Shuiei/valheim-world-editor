using HarmonyLib;
using UnityEngine;

namespace DrawnWorlds;

/// <summary>
/// The Ashlands' ground in a drawn world. The game shapes it by how near a point is to its southern edge ring (craggy
/// near it, flat and sunk away from it) and sinks it past the world's edge; drawn Ashlands are wherever the player
/// drew them, so they take the shape the game gives the ground near that ring everywhere, none sunk (the drawing's own
/// sea makes their coasts). The rest is the game's shape, step for step as the editor works it out
/// (ValheimGen.WorldGenerator.GetAshlandsHeight with a drawn land).
/// </summary>
public sealed partial class DrawnWorldsPlugin
{
    [HarmonyPatch(typeof(WorldGenerator), nameof(WorldGenerator.GetAshlandsHeight))]
    private static class AshlandsHeight
    {
        private static bool Prefix(float wx, float wy, bool cheap, float ___m_offset3, FastNoise ___m_noiseGen, out Color mask, ref float __result)
        {
            mask = Color.black;
            if (Land == null)
            {
                return true;
            }
            __result = Drawn(wx, wy, cheap, ___m_offset3, ___m_noiseGen, out mask);
            return false;
        }
    }

    /// <summary>The Ashlands' height at a point of a drawn world, in the generator's units, and its lava (alpha).</summary>
    private static float Drawn(float wx, float wy, bool cheap, float offset, FastNoise noise, out Color mask)
    {
        double px = wx, pz = wy;
        double land = DrawnBaseHeight((float)px, (float)pz);
        const double nearEdge = 1.0, inside = 1.0;
        px += (double)(100000f + offset);
        pz += (double)(100000f + offset);
        double cells = 0.0, weight = 1.0, freq = (double)0.33f;
        for (int i = 0, n = cheap ? 2 : 5; i < n; i++)
        {
            cells += weight * DUtils.MathfLikeSmoothStep(0.0, 1.0, noise.GetCellular(px * freq, pz * freq));
            freq *= 2.0;
            weight *= 0.5;
        }
        cells = DUtils.Remap(cells, -1.0, 1.0, 0.0, 1.0);
        double crags = DUtils.Lerp(nearEdge, DUtils.BlendOverlay(nearEdge, cells), 0.5);
        double h = DUtils.Lerp(land, (double)0.15f, 0.75);
        h += crags * 0.5;
        h = DUtils.Lerp(-1.0, h, DUtils.MathfLikeSmoothStep(0.0, 1.0, inside));
        const double lavaLevel = 0.15;
        double cracks = 0.0;
        weight = 1.0;
        freq = 8.0;
        for (int i = 0, n = cheap ? 2 : 3; i < n; i++)
        {
            cracks += weight * noise.GetCellular(px * freq, pz * freq);
            freq *= 2.0;
            weight *= 0.5;
        }
        cracks = DUtils.Remap(cracks, -1.0, 1.0, 0.0, 1.0);
        cracks = DUtils.Clamp01(Math.Pow(cracks, 4.0) * 2.0);
        double swell = Math.Pow(DUtils.Remap(noise.GetSimplexFractal(px * 0.075, pz * 0.075), -1.0, 1.0, 0.0, 1.0), (double)1.4f);
        h *= swell;
        double lava = DUtils.Fbm(new Vector2((float)(px * (double)0.01f), (float)(pz * (double)0.01f)), 3, 2.0, 0.5);
        lava *= DUtils.Clamp01(DUtils.Remap(nearEdge, 0.0, 0.5, 0.5, 1.0));
        lava = Math.Pow(DUtils.LerpStep((double)0.7f, 1.0, lava), 2.0);
        lava = DUtils.BlendOverlay(lava, cracks);
        lava *= DUtils.Clamp01((h - lavaLevel - 0.02) / 0.01);
        double sink = DUtils.Remap(Math.Pow(DUtils.PerlinNoise(px * 0.05 + 5124.0, pz * 0.05 + 5000.0), 2.0), 0.0, 1.0, (double)0.01f, (double)0.055f);
        double floor = Mathf.Clamp((float)(h - sink), (float)(lavaLevel + (double)0.01f), 5000f);
        h = DUtils.Lerp(h, floor, lava);
        mask = new Color(0f, 0f, 0f, (float)lava);
        return (float)h;
    }
}
