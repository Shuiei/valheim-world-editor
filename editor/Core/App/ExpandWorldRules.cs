using ValheimGen;
using ValheimWorldEditor.Sdk;

namespace TerrainEditor.App;

/// <summary>
/// The editor's own world rules provider: Expand World's (<see cref="ExpandWorld"/>), while the Expand World switch in
/// Settings is on (a world made with Expand World Size opens with the game's own rules otherwise, said in the log).
/// </summary>
/// <param name="host">The editor, as the readers ask it.</param>
/// <param name="settings">Gives the editor's settings (the switch, and the Expand World folder).</param>
internal sealed class ExpandWorldRules(IEditorHost host, Func<AppSettings> settings) : IWorldRulesProvider
{
    /// <inheritdoc/>
    public async Task<WorldRules?> ForWorld(WorldRulesRequest world)
    {
        var s = settings();
        if (!s.ExpandWorld)
        {
            ExpandWorld.NoteSwitchOff(world.Name, ExpandWorld.Keys(world.StartingGlobalKeys).Count);
            return null;
        }
        return await ExpandWorld.Resolve(world, host, s.ExpandWorldFolder).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public Task<WorldRules?> ForNewWorld()
    {
        var s = settings();
        return Task.FromResult(s.ExpandWorld ? ExpandWorld.ForNewWorld(host.BepInExFolders, s.ExpandWorldFolder) : null);
    }
}
