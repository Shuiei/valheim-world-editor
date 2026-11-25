using Rnd = ValheimGen.UnityEngine.Random;

namespace ValheimGen;

/// <summary>
/// Which parts of a blueprint are made, as Expand World Data 1.81 rolls them (BlueprintPlan.Add): first one draw per
/// group, in the order the groups first appear, that picks at most one of its parts; then, part by part, a part of a
/// group is made when it is its group's pick, any other by its own chance (drawn only below 1). Groups are each
/// blueprint's own, so a blueprint in a part's place rolls its own groups when it is reached.
/// </summary>
public static class BlueprintGroups
{
    /// <summary>
    /// Each group's pick (BlueprintPlan.PickGroups): the draw times the group's weights' total (at least 1) taken down
    /// by each weight in turn, the part that takes it below 0 picked; none when the weights add up to less than the draw.
    /// </summary>
    /// <param name="parts">The blueprint's parts.</param>
    public static Dictionary<string, BlueprintPart?> Pick(IReadOnlyList<BlueprintPart> parts)
    {
        var picked = new Dictionary<string, BlueprintPart?>(StringComparer.Ordinal);
        foreach (var members in parts.Where(p => !string.IsNullOrEmpty(p.Group)).GroupBy(p => p.Group!, StringComparer.Ordinal))
        {
            float total = Math.Max(1f, members.Sum(p => p.Chance));
            float value = Rnd.Value * total;
            BlueprintPart? pick = null;
            foreach (var member in members)
            {
                value -= member.Chance;
                if (value < 0f)
                {
                    pick = member;
                    break;
                }
            }
            picked[members.Key] = pick;
        }
        return picked;
    }

    /// <summary>Whether a part is made: its group's pick, else by its own chance (a draw only below 1).</summary>
    /// <param name="part">The part.</param>
    /// <param name="picked">Its blueprint's picks (<see cref="Pick"/>).</param>
    public static bool Made(BlueprintPart part, Dictionary<string, BlueprintPart?> picked) =>
        string.IsNullOrEmpty(part.Group) ? part.Chance >= 1f || Rnd.Value <= part.Chance : ReferenceEquals(picked[part.Group], part);
}
