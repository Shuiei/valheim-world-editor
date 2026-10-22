using ValheimGen;

namespace ExpandWorld.WorldEditor;

// The status effects of the biome and territory files' entries.
public static partial class ExpandWorldMod
{
    /// <summary>
    /// The status effects an entry gives ("statusEffects": a list of maps), as the mod reads each one: its name, by
    /// day and by night (neither said: both); an item that is no map is passed over.
    /// </summary>
    /// <param name="f">The entry's fields.</param>
    private static List<StatusEffect> StatusEffectsOf(Fields f)
    {
        List<StatusEffect> effects = [];
        if (f.Raw("statusEffects") is not List<object?> items)
        {
            return effects;
        }
        foreach (var map in items.OfType<Dictionary<string, object?>>())
        {
            var s = f.Of(map);
            bool day = s.B("day", false), night = s.B("night", false);
            bool both = !day && !night;
            effects.Add(new StatusEffect(s.Str("name"), day || both, night || both));
        }
        return effects;
    }
}
