namespace TerrainEditor.App;

public static partial class GameCatalogues
{
    private sealed partial class DungeonScan
    {
        /// <summary>
        /// What a spawn, pick, generator, location or teleport script is to the scan, by its fields. Null: none.
        /// </summary>
        private static string? ComponentKind(HashSet<string> f) =>
            f.Contains("m_chanceToSpawn") && f.Contains("m_OffObject") ? "spawn"
            : f.Contains("m_objects") && f.Contains("m_dungeonRequireTheme") && !f.Contains("m_chanceToSpawn") ? "pick"
            : f.Contains("m_algorithm") && f.Contains("m_themes") && f.Contains("m_maxRooms") ? "generator"
            : f.Contains("m_exteriorRadius") && f.Contains("m_clearArea") ? "location"
            : f.Contains("m_targetPoint") && f.Contains("m_enterText") ? "teleport"
            : null;
    }
}
