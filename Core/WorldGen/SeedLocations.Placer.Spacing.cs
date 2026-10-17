
namespace ValheimGen;

public static partial class SeedLocations
{
    internal sealed partial class Placer
    {
        /// <summary>
        /// Far enough from its own prefab or group, and near enough to its own prefab or "max" group when it
        /// needs one.
        /// </summary>
        internal bool RightNeighbours(Rule rule, Vector3 spot)
        {
            if (rule.minDistanceFromSimilar > 0f && (_byPrefab.Within(rule.prefab, spot, rule.minDistanceFromSimilar) || (rule.group.Length > 0 && _byGroup.Within(rule.group, spot, rule.minDistanceFromSimilar))))
            {
                return false;
            }
            return !(rule.maxDistanceFromSimilar > 0f && !_byPrefab.Within(rule.prefab, spot, rule.maxDistanceFromSimilar) && !(rule.groupMax.Length > 0 && _byGroupMax.Within(rule.groupMax, spot, rule.maxDistanceFromSimilar)));
        }

        /// <summary>
        /// The ground's vegetation within the kind's bounds (either end left out when not set).
        /// </summary>
        internal static bool RightVegetation(Rule rule, float vegetation) =>
            !(rule.minimumVegetation > 0f && vegetation <= rule.minimumVegetation) && !(rule.maximumVegetation < 1f && vegetation >= rule.maximumVegetation);

        /// <summary>
        /// Kinds that want the greenest spots: the vegetation on rings around the spot (nearer counts
        /// more) must beat the average of the kind's spots so far by a share of the way to the best; the
        /// first nine spots only set the bar.
        /// </summary>
        internal bool StandsOut(Rule rule, Vector3 spot)
        {
            if (rule.surroundCheckVegetation == 0)
            {
                return true;
            }
            float total = 0f, far = rule.surroundCheckDistance;
            for (int ring = 1; ring <= rule.surroundCheckLayers; ring++)
            {
                float r = (float)ring / rule.surroundCheckLayers * far;
                float weight = (far - r) / (far * 2f);
                for (int k = 0; k < 6; k++)
                {
                    float angle = k / 6f * MathF.PI * 2f;
                    _gen.GetHeight(spot.x + MathF.Sin(angle) * r, spot.z + MathF.Cos(angle) * r, out Color around);
                    total += around.a * weight;
                }
            }
            _sums++;
            _sumsTotal += total;
            if (total > _sumsBest)
            {
                _sumsBest = total;
            }
            if (_sums < 10)
            {
                return false;
            }
            float best = _sumsBest, average = (float)(_sumsTotal / _sums);
            return total >= average + (best - average) * rule.surroundBetterThanAverage;
        }
    }
}
