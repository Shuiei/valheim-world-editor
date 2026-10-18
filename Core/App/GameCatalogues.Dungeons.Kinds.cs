namespace TerrainEditor.App;

public static partial class GameCatalogues
{
    private sealed partial class DungeonScan
    {
        /// <summary>
        /// What a script is to the scan, by its fields: the first kind that matches. Null: none that matters.
        /// </summary>
        private static string? Kind(HashSet<string> f) => PrefabKind(f) ?? ComponentKind(f);

        /// <summary>
        /// What a room, connection or ZNetView script is to the scan, by its fields. Null: none.
        /// </summary>
        private static string? PrefabKind(HashSet<string> f) =>
            f.Contains("m_theme") && f.Contains("m_endCap") && f.Contains("m_size") ? "room"
            : f.Contains("m_entrance") && f.Contains("m_allowDoor") ? "connection"
            : f.Contains("m_persistent") && f.Contains("m_distant") ? "netview"
            : null;
    }
}
