using Avalonia.Headless.XUnit;
using Avalonia.Input;
using TerrainEditor.App;
using Xunit;
using static TerrainEditor.Desktop.Tests.SelectToolRun;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// What a move "put on the ground" may land on (SelectTool.DropTargets): tops that hold something up, not a tree's.
/// </summary>
[Collection("World files")]
public class SelectToolDropTargetsTests
{
    // A tree moved onto another as tall (in a wood) lands on the ground under it: it landed on the other tree's top,
    // 5 m up here, and some 40 m up among the game's beeches.
    [AvaloniaFact]
    public void ATreeMovedInAWoodLandsOnTheGround()
    {
        var v = Open(T(1, Beech, 40, 30, 40, false), T(2, Beech, 50, 30, 50, false));
        v.Tool.OnGround = true;
        v.Click(40, 40);
        v.Drag(KeyModifiers.None, (40, 40), (45, 45), (50, 50));
        Assert.Equal(50, v.Last.Position.X, 1);
        Assert.Equal(30, v.Last.Position.Y, 2);
    }

    [Fact]
    public void FloorsRocksAndRuinsHoldThingsUpTreesBushesAndCreaturesDoNot()
    {
        Assert.All([ObjectKind.Buildings, ObjectKind.Ruins, ObjectKind.Furniture, ObjectKind.Rocks, ObjectKind.Ore], k => Assert.True(SelectTool.HasTop(k)));
        Assert.All([ObjectKind.Trees, ObjectKind.Bushes, ObjectKind.Pickables, ObjectKind.Creatures, ObjectKind.Animals, ObjectKind.Effects], k => Assert.False(SelectTool.HasTop(k)));
    }
}
