using TerrainEditor.App;

namespace TerrainEditor.Editing;

public static partial class DungeonEntrances
{
    /// <summary>
    /// What a location is called as an entrance, in the language in use and in English: a burial chamber, a Frost
    /// Cave... (its own name, the same in both, when it has no other).
    /// </summary>
    public static Said Describe(LocationPrefabs.Prefab p) => Array.Find(Locations, l => l.Name == p.Name).Description switch
    {
        Called.BurialChamber => Said.Of("dungeon.entrance.burialChamber"),
        Called.SunkenCrypt => Said.Of("dungeon.entrance.sunkenCrypt"),
        Called.FrostCave => Said.Of("dungeon.kind.frostCave"),
        Called.InfestedMine => Said.Of("dungeon.entrance.infestedMine"),
        Called.MorgenHole => Said.Of("dungeon.entrance.morgenHole"),
        _ => Said.Raw(p.Name),
    };
}
