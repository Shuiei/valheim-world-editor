namespace TerrainEditor.App;

public static partial class GameCatalogues
{
    private sealed partial class DungeonScan
    {
        /// <summary>
        /// What a spawn, pick, generator, location, teleport or snap-to-ground script is to the scan, by its fields. Null:
        /// none.
        /// </summary>
        private static string? ComponentKind(HashSet<string> f) =>
            f.Contains("m_chanceToSpawn") && f.Contains("m_OffObject") ? "spawn"
            : f.Contains("m_objects") && f.Contains("m_dungeonRequireTheme") && !f.Contains("m_chanceToSpawn") ? "pick"
            : f.Contains("m_algorithm") && f.Contains("m_themes") && f.Contains("m_maxRooms") ? "generator"
            : f.Contains("m_exteriorRadius") && f.Contains("m_clearArea") ? "location"
            : f.Contains("m_targetPoint") && f.Contains("m_enterText") ? "teleport"
            : SnapKind(f);

        /// <summary>"snap" for the game's SnapToGround (<see cref="IsSnapper"/>); null for any other script.</summary>
        private static string? SnapKind(HashSet<string> f) => IsSnapper(f) ? "snap" : null;

        /// <summary>
        /// Whether a script's fields are those of the game's SnapToGround: an offset alone, besides what every
        /// MonoBehaviour has.
        /// </summary>
        private static bool IsSnapper(HashSet<string> f) => f.Contains("m_offset") && f.Except(BehaviourFields).Count() == 1;

        /// <summary>The fields every MonoBehaviour has.</summary>
        private static readonly string[] BehaviourFields = ["m_GameObject", "m_Enabled", "m_Script", "m_Name", "m_EditorHideFlags", "m_EditorClassIdentifier"];
    }
}
