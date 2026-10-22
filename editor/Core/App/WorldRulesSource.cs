using TerrainEditor.Save;
using ValheimGen;
using ValheimWorldEditor.Sdk;

namespace TerrainEditor.App;

/// <summary>
/// The rules worlds are generated with: the first world rules provider giving any, the running extensions' (in the
/// order they registered) then the editor's own (<see cref="ExpandWorldRules"/>); the game's own when none does.
/// </summary>
public static class WorldRulesSource
{
    /// <summary>A world's rules, waiting for the providers without holding a thread.</summary>
    /// <param name="save">The world as read (its name, starting global keys, loaded keys and grid).</param>
    /// <param name="live">The game it runs in, when live; null for a saved world.</param>
    /// <param name="settings">The editor's settings.</param>
    public static async Task<WorldRules> ForWorld(WorldSave save, ILiveGame? live, AppSettings settings)
    {
        var world = new WorldRulesRequest(save.Name, [.. save.StartingGlobalKeys], save.Zones?.GlobalKeys.ToList(), save.GridWidth, live);
        return await First(p => p.ForWorld(world), settings).ConfigureAwait(false);
    }

    /// <summary>The rules a new world is made and looked at with (the New world page). Waits for the providers away from the caller's thread.</summary>
    /// <param name="settings">The editor's settings.</param>
    public static WorldRules ForNewWorld(AppSettings settings) => Task.Run(() => First(p => p.ForNewWorld(), settings)).GetAwaiter().GetResult();

    /// <summary>The first rules other than the game's own the providers give; the game's own when none does.</summary>
    private static async Task<WorldRules> First(Func<IWorldRulesProvider, Task<WorldRules?>> ask, AppSettings settings) =>
        await ModExtensions.FirstRules(ask).ConfigureAwait(false)
        ?? await BuiltIn(ask, new ExpandWorldRules(new EditorHost(() => settings), () => settings)).ConfigureAwait(false)
        ?? WorldRules.Vanilla;

    /// <summary>
    /// The editor's own provider's rules, faults held as an extension's are: one that throws leaves the game's own
    /// rules (said in the log), so a world still opens.
    /// </summary>
    /// <param name="ask">Asks the provider.</param>
    /// <param name="provider">The editor's own provider.</param>
    internal static async Task<WorldRules?> BuiltIn(Func<IWorldRulesProvider, Task<WorldRules?>> ask, IWorldRulesProvider provider)
    {
        try
        {
            return await ask(provider).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            // Whatever reading the mods' files throws that the readers do not hold themselves: the world is not held up.
            Log.Caught(e, "working out the world's Expand World rules (the game's own are used)");
            return null;
        }
    }
}
