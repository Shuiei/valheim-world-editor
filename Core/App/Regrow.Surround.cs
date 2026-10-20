using Rule = ValheimGen.VegetationRule;

namespace TerrainEditor.App;

// The vegetation round a spot, for the rules that want the greenest (or the barest) spots of their zone.
public static partial class Regrow
{
    /// <summary>The game's last checks of a spot, in its order: the surround check, then the clear areas.</summary>
    private static bool PassesLastChecks(Pass pass, Rule veg, Zone hm, System.Numerics.Vector3 p) =>
        PassesSurround(pass, veg, hm, p.X, p.Z) && PassesClearAreas(pass, veg, p.X, p.Y, p.Z);

    /// <summary>
    /// PlaceVegetation's surround check, for a rule that asks for it: the vegetation mask summed on rings of six points
    /// round the spot, each ring weighted as the game weights it ((1 - its radius) / (the distance x 2): the game's own
    /// formula, which the location check writes differently), must reach the rule's share of the way from the average
    /// of the zone's sums for this rule so far to their best; the first nine spots only set the bar. The mask is read
    /// from the spot's zone (clamped at its edge, as the game's heightmap reads it). Floats as the game computes them:
    /// in double, rounded at each store.
    /// </summary>
    private static bool PassesSurround(Pass pass, Rule veg, Zone hm, float px, float pz)
    {
        if (veg.surroundCheckVegetation == 0)
        {
            return true;
        }
        float sum = 0f, distance = veg.surroundCheckDistance;
        for (int layer = 0; layer < veg.surroundCheckLayers; layer++)
        {
            float r = (float)((double)(layer + 1) / veg.surroundCheckLayers * distance);
            float weight = (float)((1.0 - r) / ((double)distance * 2.0));
            for (int k = 0; k < 6; k++)
            {
                float f = (float)(k / 6.0 * MathF.PI * 2.0);
                float x = px + (float)((double)(float)Math.Sin(f) * r), z = pz + (float)((double)(float)Math.Cos(f) * r);
                sum = (float)((double)sum + (double)hm.VegetationMask(x, z) * weight);
            }
        }
        pass.Sums.Add(sum);
        if (pass.Sums.Count < 10)
        {
            return false;
        }
        float best = pass.Sums.Max(), average = pass.Sums.Average();
        return sum >= (float)((double)average + ((double)best - average) * veg.surroundBetterThanAverage);
    }
}
