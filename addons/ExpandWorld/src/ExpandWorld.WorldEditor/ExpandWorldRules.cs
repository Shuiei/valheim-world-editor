using ValheimGen;
using ValheimWorldEditor.Sdk;

namespace ExpandWorld.WorldEditor;

/// <summary>
/// The add-on's world rules provider: Expand World's rules (<see cref="ExpandWorldMod"/>) for a world and a new world,
/// from the mods in the BepInEx folders the editor knows, or a live game's own files; and the mod's name on the cards of
/// worlds made with Expand World Size.
/// </summary>
/// <param name="host">The editor.</param>
internal sealed class ExpandWorldRules(IEditorHost host) : IWorldRulesProvider
{
    /// <inheritdoc/>
    public async Task<WorldRules?> ForWorld(WorldRulesRequest world) => await ExpandWorldMod.Resolve(world, host).ConfigureAwait(false);

    /// <inheritdoc/>
    public Task<WorldRules?> ForNewWorld() => Task.FromResult<WorldRules?>(ExpandWorldMod.ForNewWorld(host.BepInExFolders));

    /// <inheritdoc/>
    public string? MadeBy(IReadOnlyList<string> startingGlobalKeys) => ExpandWorldMod.Keys(startingGlobalKeys).Count > 0 ? "Expand World" : null;
}
