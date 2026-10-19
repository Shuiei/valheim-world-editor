namespace ValheimGen;

// Expand World Data's world rules (its BiomeCalculator): which rule a point passes, and the heat of the sea past a
// boiling rule's edge. Floats as the mod's: each expression worked out in double and rounded where the mod stores it.
public sealed partial class WorldGenerator
{
    /// <summary>
    /// The biome the world rules give a point (the mod's prefix on the game's GetBiome): the first rule the point
    /// passes, else the ocean. In the generator's coordinates (<see cref="Stretched"/>).
    /// </summary>
    private Heightmap.Biome RulesBiome(float x, float z, float oceanLevel, bool waterAlwaysOcean)
    {
        // The game's GetHeight, given the point as it is here: it stretches it again on its own way in.
        if (waterAlwaysOcean && GetHeight(x, z) <= oceanLevel)
        {
            return Heightmap.Biome.Ocean;
        }
        return RuleAt(_v.BiomeRules, x, z, Mathf.Atan2(x, z), _v.Legacy)?.Biome ?? Heightmap.Biome.Ocean;
    }

    /// <summary>
    /// The territory at a point: the first territory rule it passes, when the territory file knows the name; null for
    /// none. In the generator's coordinates.
    /// </summary>
    internal Territory? TerritoryAt(float x, float z)
    {
        if (_v.TerritoryRules.Count == 0)
        {
            return null;
        }
        var rule = RuleAt(_v.TerritoryRules, x, z, Mathf.Atan2(x, z), legacy: false);
        return rule == null ? null : _v.Territories.GetValueOrDefault(rule.Territory);
    }

    /// <summary>
    /// How hot the sea is at a point under the world rules (the mod's GetBoiling, which stands in for the game's
    /// "is Ashlands" and its ocean gradient): the rule's boiling times the metres past its inner edge (or its
    /// rectangle's) over 300; -1 where the rule the point passes does not boil, or none does.
    /// </summary>
    private float Boiling(float wx, float wy)
    {
        float angle = Mathf.Atan2(wx, wy);
        var rule = RuleAt(_v.BiomeRules, wx, wy, angle, legacy: false);
        if (rule == null || rule.Boiling <= 0f)
        {
            return -1f;
        }
        float sx = (float)((double)wx * _v.Stretch), sy = (float)((double)wy * _v.Stretch);
        float dist = GenMath.Length((float)((double)sx - rule.CenterX), (float)((double)sy - rule.CenterY));
        float edge = (float)((double)dist - InnerEdge(rule, angle));
        if (rule.HasRectangle)
        {
            float rectEdge = RectangleEdge(rule, sx, sy);
            edge = rule.MinDistance > 0f ? Mathf.Min(edge, rectEdge) : rectEdge;
        }
        return (float)((double)rule.Boiling * edge / 300.0);
    }

    /// <summary>
    /// The first rule of a list a point passes (the mod's GetEntry, and GetLegacy with <paramref name="legacy"/>): its
    /// altitude within the rule's limits, its distance from the middle (or the rule's centre, waved) within them, in
    /// the rule's rectangle and sector when it has them, and its noise field high enough for the rule's amount.
    /// </summary>
    /// <param name="rules">The rules, in order.</param>
    /// <param name="wx">The point's x, in the generator's coordinates.</param>
    /// <param name="wy">The point's z.</param>
    /// <param name="worldAngle">The point's angle from the z axis, radians.</param>
    /// <param name="legacy">The older evaluation: altitude limits inclusive, the outer edge inside, no rectangles, the
    /// noise test strict.</param>
    private BiomeRule? RuleAt(IReadOnlyList<BiomeRule> rules, float wx, float wy, float worldAngle, bool legacy)
    {
        float sx = (float)((double)wx * _v.Stretch), sy = (float)((double)wy * _v.Stretch);
        float magnitude = GenMath.Length(sx, sy);
        if (magnitude > _v.TotalRadius)
        {
            return null;
        }
        // The mod's BaseHeightToAltitude of the game's GetBaseHeight (with Expand World Size's transform on it).
        float altitude = (float)((double)Continent(wx, wy) * 200.0 - _v.WaterLevel);
        float baseAngle = _v.CheckSectors ? (float)(((double)worldAngle + Mathf.PI) / 2.0 / Mathf.PI) : 0f;
        float bx = (float)((double)wx / _v.BiomeStretchF), by = (float)((double)wy / _v.BiomeStretchF);
        foreach (var rule in rules)
        {
            if (legacy ? rule.MinAltitude > altitude || rule.MaxAltitude < altitude : rule.MinAltitude >= altitude || rule.MaxAltitude <= altitude)
            {
                continue;
            }
            float mag = rule.CenterX != 0f || rule.CenterY != 0f ? GenMath.Length((float)((double)sx - rule.CenterX), (float)((double)sy - rule.CenterY)) : magnitude;
            float max = rule.MaxDistance;
            if (!(mag > InnerEdge(rule, worldAngle) && (max >= _v.Radius || (legacy ? mag <= max : mag < max))))
            {
                continue;
            }
            if (!legacy && rule.HasRectangle && RectangleEdge(rule, sx, sy) <= 0f)
            {
                continue;
            }
            if (_v.CheckSectors && !InSector(rule, baseAngle, magnitude))
            {
                continue;
            }
            if (rule.Amount < 1f && !PassesNoise(rule, bx, by, legacy))
            {
                continue;
            }
            return rule;
        }
        return null;
    }
}
