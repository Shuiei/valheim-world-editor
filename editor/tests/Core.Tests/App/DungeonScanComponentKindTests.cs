using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// What a script is to the dungeon scan by its fields (DungeonScan.ComponentKind): a SnapToGround is an offset alone,
/// besides what every MonoBehaviour has.
/// </summary>
public class DungeonScanComponentKindTests
{
    private static string? Kind(params string[] fields) =>
        (string?)PrivateCall.Static(PrivateMember.Nested(typeof(GameCatalogues), "DungeonScan"), "ComponentKind", fields.ToHashSet());

    [Fact]
    public void AnOffsetAloneIsASnapper()
    {
        Assert.Equal("snap", Kind("m_GameObject", "m_Enabled", "m_Script", "m_Name", "m_offset"));
        // An offset among other fields is some other script.
        Assert.Null(Kind("m_GameObject", "m_Enabled", "m_Script", "m_Name", "m_offset", "m_radius"));
        Assert.Null(Kind("m_GameObject", "m_Enabled", "m_Script", "m_Name"));
    }
}
