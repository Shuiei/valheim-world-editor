using TerrainEditor.Save;
using ValheimGen;
using ValheimWorldEditor.Sdk;

namespace TerrainEditor.App;

/// <summary>
/// The rules worlds are generated with: the first the running extensions' world rules providers give (in the order
/// they registered; an add-on's, as Expand World's); the game's own when none does.
/// </summary>
public static class WorldRulesSource
{
    /// <summary>A world's rules, waiting for the providers without holding a thread.</summary>
    /// <param name="save">The world as read (its name, starting global keys, loaded keys and grid).</param>
    /// <param name="live">The game it runs in, when live; null for a saved world.</param>
    public static async Task<WorldRules> ForWorld(WorldSave save, ILiveGame? live)
    {
        var world = new WorldRulesRequest(save.Name, [.. save.StartingGlobalKeys], save.Zones?.GlobalKeys.ToList(), save.GridWidth, live);
        return await ModExtensions.FirstRules(p => p.ForWorld(world)).ConfigureAwait(false) ?? WorldRules.Vanilla;
    }

    /// <summary>The rules a new world is made and looked at with (the New world page). Waits for the providers away from the caller's thread.</summary>
    public static WorldRules ForNewWorld() => Task.Run(() => ModExtensions.FirstRules(p => p.ForNewWorld())).GetAwaiter().GetResult() ?? WorldRules.Vanilla;
}
