namespace TerrainEditor.App;

/// <summary>
/// The mods' prefabs, from either source: what a live game reported (ModCatalogue) and the installed mods read without
/// it (ModScan), the live game's word first.
/// </summary>
public static class ModPrefabs
{
    /// <summary>Whether a prefab is a mod's (a live game reported it, or an installed mod has it), not the game's own.</summary>
    public static bool IsModded(string prefab) => ModCatalogue.IsModded(prefab) || ModScan.IsModded(prefab);

    /// <summary>The mod that adds a prefab; null for the game's own and when neither source knows it.</summary>
    public static string? ModOf(string prefab) => ModCatalogue.ModOf(prefab) ?? ModScan.ModOf(prefab);
}
