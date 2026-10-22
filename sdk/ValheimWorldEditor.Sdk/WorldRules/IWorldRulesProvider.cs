using System.Threading.Tasks;
using ValheimGen;

namespace ValheimWorldEditor.Sdk;

/// <summary>
/// Gives the rules worlds are generated with (register it with <see cref="IExtensionRegistry.AddWorldRules"/>): a
/// world generation mod's settings and data files (Expand World's) read into the editor's world-rules model
/// (<see cref="WorldRules"/>), which the editor's generator follows. The first provider giving rules for a world has
/// them used; with none, the world is the game's own.
/// </summary>
public interface IWorldRulesProvider
{
    /// <summary>
    /// The rules a world opens with: one on this computer, or a live game's (<see cref="WorldRulesRequest.Live"/>: its
    /// own files, through its bridge). Null when the mod does not change the world (not installed, or at its
    /// defaults; <see cref="WorldRules.Vanilla"/> says the same). Throwing is a fault: the extension is turned off, and the
    /// next provider is asked.
    /// </summary>
    /// <param name="world">The world.</param>
    Task<WorldRules?> ForWorld(WorldRulesRequest world);

    /// <summary>The rules a new world is made and looked at with (the New world page); null for the game's own.</summary>
    Task<WorldRules?> ForNewWorld();
}
