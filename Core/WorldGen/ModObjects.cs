namespace ValheimGen;

/// <summary>
/// How Expand World Data turns an object it spawns into another and gives it data (its RandomizeSwap, RandomizeData and
/// GetData), on the game's random state replica: shared by New world's locations and the Dungeon tool's rooms.
/// </summary>
public static class ModObjects
{
    /// <summary>
    /// A weighted choice as the mod draws it: the only one without a draw, else one draw taken down by each weight until
    /// it reaches 0, the last when it never does. No choice is the mod's empty pick (nothing for a swap: the object is
    /// removed). The draw is the game's seeded generator's; nothing here is security-sensitive.
    /// </summary>
    /// <typeparam name="T">The choice.</typeparam>
    /// <param name="choices">The choices.</param>
    public static T? Pick<T>(IReadOnlyList<Weighted<T>> choices)
    {
        if (choices.Count == 0)
        {
            return typeof(T) == typeof(string) ? (T)(object)"" : default;
        }
        if (choices.Count == 1)
        {
            return choices[0].Value;
        }
        float roll = UnityEngine.Random.Value;
        foreach (var c in choices)
        {
            roll = (float)((double)roll - c.Weight);
            if (roll <= 0f)
            {
                return c.Value;
            }
        }
        return choices[^1].Value;
    }

    /// <summary>A prefab swapped as the swaps say (a draw when there are several choices); "" when it is removed.</summary>
    /// <param name="prefab">The prefab.</param>
    /// <param name="swaps">The swaps, by prefab.</param>
    public static string Swapped(string prefab, IReadOnlyDictionary<string, IReadOnlyList<Weighted<string>>> swaps) =>
        swaps.TryGetValue(prefab, out var choices) ? Pick(choices) ?? "" : prefab;

    /// <summary>Adds the data the choices give an object: those for "all", then those for its prefab (a draw each when there are several).</summary>
    /// <param name="fields">Where the data goes.</param>
    /// <param name="data">The data choices, by prefab or "all".</param>
    /// <param name="prefab">The object's prefab.</param>
    public static void AddData(List<ContentField> fields, IReadOnlyDictionary<string, IReadOnlyList<Weighted<IReadOnlyList<ContentField>?>>> data, string prefab)
    {
        foreach (string key in new[] { "all", prefab })
        {
            if (data.TryGetValue(key, out var choices) && Pick(choices) is { } values)
            {
                fields.AddRange(values);
            }
        }
    }
}
