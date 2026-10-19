
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
            if (ModSpacing)
            {
                return RightNeighboursByMod(rule, spot);
            }
            if (rule.MinDistanceFromSimilar > 0f && (_byPrefab.Within(rule.Prefab, spot, rule.MinDistanceFromSimilar) || (rule.Group.Length > 0 && _byGroup.Within(rule.Group, spot, rule.MinDistanceFromSimilar))))
            {
                return false;
            }
            return !(rule.MaxDistanceFromSimilar > 0f && !_byPrefab.Within(rule.Prefab, spot, rule.MaxDistanceFromSimilar) && !(rule.GroupMax.Length > 0 && _byGroupMax.Within(rule.GroupMax, spot, rule.MaxDistanceFromSimilar)));
        }

        /// <summary>
        /// Expand World Data's distance checks (its HaveLocationInRange, with the location file in use): "similar" is
        /// the same game prefab (a clone counts) or a kind whose groups hold the group asked for; a kind with "awayFrom"
        /// is kept away from any of those kinds or groups, one with "closeTo" needs one of each row within reach.
        /// </summary>
        private bool RightNeighboursByMod(Rule rule, Vector3 spot)
        {
            if (rule.MinDistanceFromSimilar > 0f && (rule.AwayFrom != null ? rule.AwayFrom.Any(r => Near(r, spot)) : Similar(rule.Base, rule.Group, spot, rule.MinDistanceFromSimilar)))
            {
                return false;
            }
            if (rule.MaxDistanceFromSimilar > 0f)
            {
                return rule.CloseTo != null ? rule.CloseTo.All(row => row.Any(r => Near(r, spot))) : Similar(rule.Base, rule.GroupMax, spot, rule.MaxDistanceFromSimilar);
            }
            return true;
        }

        /// <summary>An instance of the game prefab, or of a kind in the group (when one is named), closer than distance.</summary>
        private bool Similar(string basePrefab, string group, Vector3 spot, float distance) =>
            _byBase.Within(basePrefab, spot, distance) || (group.Length > 0 && _byGroups.Within(group, spot, distance));

        /// <summary>An instance of the rule's kind (by its full name) or of a kind in its group, closer than its distance.</summary>
        private bool Near(DistanceRule r, Vector3 spot) => _byPrefab.Within(r.Name, spot, r.Distance) || _byGroups.Within(r.Name, spot, r.Distance);

        /// <summary>
        /// The ground's vegetation within the kind's bounds (either end left out when not set).
        /// </summary>
        internal static bool RightVegetation(Rule rule, float vegetation) =>
            !(rule.MinimumVegetation > 0f && vegetation <= rule.MinimumVegetation) && !(rule.MaximumVegetation < 1f && vegetation >= rule.MaximumVegetation);

        /// <summary>
        /// Kinds that want the greenest spots: the vegetation on rings around the spot (nearer counts
        /// more) must beat the average of the kind's spots so far by a share of the way to the best; the
        /// first nine spots only set the bar.
        /// </summary>
        internal bool StandsOut(Rule rule, Vector3 spot)
        {
            if (rule.SurroundCheckVegetation == 0)
            {
                return true;
            }
            float total = 0f, far = rule.SurroundCheckDistance;
            for (int ring = 1; ring <= rule.SurroundCheckLayers; ring++)
            {
                float r = (float)ring / rule.SurroundCheckLayers * far;
                float weight = (far - r) / (far * 2f);
                for (int k = 0; k < 6; k++)
                {
                    float angle = k / 6f * MathF.PI * 2f;
                    _gen.GetHeight(spot.X + MathF.Sin(angle) * r, spot.Z + MathF.Cos(angle) * r, out Color around);
                    total += around.A * weight;
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
            return total >= average + (best - average) * rule.SurroundBetterThanAverage;
        }
    }
}
