using System.Reflection;
using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// What a dungeon prefab's script is to the scan, by the fields it has (GameCatalogues.DungeonScan.PrefabKind): a room,
/// a connection or a ZNetView, the first that matches, and nothing when the fields do not make one.
/// </summary>
public class GameCataloguesDungeonPrefabKindTests
{
    private static string? PrefabKind(params string[] fields) =>
        (string?)PrivateCall.Static(typeof(GameCatalogues).GetNestedType("DungeonScan", BindingFlags.NonPublic)!, "PrefabKind", new HashSet<string>(fields));

    [Fact]
    public void AScriptWithThemeEndCapAndSizeIsARoom()
    {
        Assert.Equal("room", PrefabKind("m_theme", "m_endCap", "m_size"));
    }

    [Fact]
    public void AScriptWithEntranceAndAllowDoorIsAConnection()
    {
        Assert.Equal("connection", PrefabKind("m_entrance", "m_allowDoor"));
    }

    [Fact]
    public void AScriptWithPersistentAndDistantIsANetView()
    {
        Assert.Equal("netview", PrefabKind("m_persistent", "m_distant"));
    }

    [Fact]
    public void ARoomNeedsAllThreeFields()
    {
        Assert.Null(PrefabKind("m_theme", "m_endCap"));
    }

    [Fact]
    public void ARoomWinsOverAConnectionWhenBothMatch()
    {
        Assert.Equal("room", PrefabKind("m_theme", "m_endCap", "m_size", "m_entrance", "m_allowDoor"));
    }

    [Fact]
    public void NoFieldsGiveNoKind()
    {
        Assert.Null(PrefabKind());
    }
}
