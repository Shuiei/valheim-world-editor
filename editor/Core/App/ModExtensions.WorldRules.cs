using ValheimGen;
using ValheimWorldEditor.Sdk;

namespace TerrainEditor.App;

/// <summary>The world rules providers running extensions register (IWorldRulesProvider), for WorldRulesSource.</summary>
public static partial class ModExtensions
{
    /// <summary>
    /// The first rules the running extensions' providers give (in the order they registered), other than the game's
    /// own; null when none does. A provider that throws turns its extension off, and the next is asked.
    /// </summary>
    /// <param name="ask">Asks one provider.</param>
    internal static async Task<WorldRules?> FirstRules(Func<IWorldRulesProvider, Task<WorldRules?>> ask)
    {
        foreach (var l in _state.Hooks)
        {
            foreach (var provider in l.WorldRules.ToList())
            {
                if (l.Fault != null)
                {
                    break;
                }
                try
                {
                    if (await ask(provider).ConfigureAwait(false) is { IsVanilla: false } rules)
                    {
                        return rules;
                    }
                }
                catch (Exception e)
                {
                    // Whatever the extension's code throws: it is turned off, and the world is not held up.
                    Log.Caught(e, $"asking the extension of {l.Mod} for a world's rules (turned off)");
                    TurnOff(l, $"{e.GetType().Name}: {e.Message}");
                }
            }
        }
        return null;
    }

    /// <summary>
    /// The mod a world's file says made it (IWorldRulesProvider.MadeBy), for its card: the first the running extensions'
    /// providers name; null when none does. A provider that throws turns its extension off.
    /// </summary>
    /// <param name="startingGlobalKeys">The starting global keys the world file keeps.</param>
    internal static string? MadeBy(IReadOnlyList<string> startingGlobalKeys)
    {
        foreach (var l in _state.Hooks)
        {
            foreach (var provider in l.WorldRules.ToList())
            {
                if (Guard(l, () => provider.MadeBy(startingGlobalKeys), null) is { Length: > 0 } mod)
                {
                    return mod;
                }
            }
        }
        return null;
    }
}
