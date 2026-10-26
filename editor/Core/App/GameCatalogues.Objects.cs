using AssetsTools.NET.Extra;

namespace TerrainEditor.App;

// What the editor reads of the game's other objects at its start (GameData): the prefabs, the
// vegetation rules, the terrain modifiers, the names of object data and the cave rocks. Each reader is in
// its own partial file (GameCatalogues.<Reader>.cs).
public static partial class GameCatalogues
{
    /// <summary>
    /// Unity's class id of a LODGroup.
    /// </summary>
    private const int LodGroupType = (int)AssetClassID.LODGroup;
}
