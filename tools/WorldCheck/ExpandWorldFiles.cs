using ExpandWorld.WorldEditor;
using TerrainEditor.App;

/// <summary>Expand World's files in a BepInEx folder read into a world's rules with the add-on's readers, for the checks.</summary>
internal static class ExpandWorldFiles
{
    /// <summary>
    /// The rules of a BepInEx folder's mods and files (no Expand World Size keys: a new world's), with the world's global
    /// keys, converted against the game's rules as the editor hands them to the add-on.
    /// </summary>
    /// <param name="bepInEx">The BepInEx folder with the mods' files.</param>
    /// <param name="globalKeys">The world's global keys.</param>
    internal static ValheimGen.WorldRules Rules(string bepInEx, IEnumerable<string> globalKeys)
    {
        ExpandWorldMod.Game = GameWorldData.Instance;
        var found = ExpandWorldMod.Look(bepInEx, "check");
        return ExpandWorldMod.Rules(new Dictionary<string, string>(), found, ExpandWorldMod.ReadData(found), globalKeys);
    }
}
