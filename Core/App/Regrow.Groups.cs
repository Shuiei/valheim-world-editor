using ValheimGen;
using Rnd = ValheimGen.UnityEngine.Random;
using Rule = ValheimGen.VegetationRule;

namespace TerrainEditor.App;

/// <summary>
/// The groups a vegetation rule places in a zone: how many it tries, where each group starts and its objects.
/// </summary>
public static partial class Regrow
{
    /// <summary>
    /// How many groups a rule places in its zone, drawn as the game does: below one, the chance of a single group
    /// (false when that chance is missed); else a count between its minimum and maximum.
    /// </summary>
    /// <param name="veg">The rule.</param>
    /// <param name="count">The number of groups to try to place.</param>
    private static bool TryRollGroupCount(Rule veg, out int count)
    {
        if (veg.max < 1f)
        {
            count = 1;
            return !(Rnd.Value > veg.max);
        }
        count = Rnd.Range((int)veg.min, (int)veg.max + 1);
        return true;
    }

    /// <summary>Tries the rule's groups in its zone until <paramref name="count"/> of them are placed (or the tries run out).</summary>
    private static void PlaceRule(TerrainService terrain, Ground ground, Dictionary<(int, int), Zone> zones, Rule veg, float cx, float cz, int count, List<Spot> spots)
    {
        float reach = 32f - veg.groupRadius;
        int tries = veg.forcePlacement != 0 ? count * 50 : count, placedGroups = 0;
        for (int i = 0; i < tries; i++)
        {
            float vx = Rnd.Range(cx - reach, cx + reach), vz = Rnd.Range(cz - reach, cz + reach);
            int groupSize = Rnd.Range(veg.groupSizeMin, veg.groupSizeMax + 1);
            if (PlaceGroup(terrain, ground, zones, veg, vx, vz, groupSize, spots))
            {
                placedGroups++;
            }
            if (placedGroups >= count)
            {
                break;
            }
        }
    }

    /// <summary>Places one group around its first object's spot; true when at least one of its objects was placed.</summary>
    private static bool PlaceGroup(TerrainService terrain, Ground ground, Dictionary<(int, int), Zone> zones, Rule veg, float vx, float vz, int groupSize, List<Spot> spots)
    {
        bool placed = false;
        for (int j = 0; j < groupSize; j++)
        {
            float px = vx, pz = vz;
            if (j > 0)
            {
                float f = Rnd.Value * MathF.PI * 2f, r = Rnd.Range(0f, veg.groupRadius);
                px += MathF.Sin(f) * r;
                pz += MathF.Cos(f) * r;
            }
            float ry = Rnd.Range(0, 360);
            float scale = Rnd.Range(veg.scaleMin, veg.scaleMax);
            float rx = Rnd.Range(-veg.randTilt, veg.randTilt), rz = Rnd.Range(-veg.randTilt, veg.randTilt);
            if (TryPlaceSpot(terrain, ground, zones, veg, px, pz, new SpotDraw(rx, ry, rz, scale), spots))
            {
                placed = true;
            }
        }
        return placed;
    }
}
