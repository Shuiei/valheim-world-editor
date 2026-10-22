using System.Numerics;
using Xunit;
using static TerrainEditor.Desktop.Tests.AreaBoxes;
using static TerrainEditor.Desktop.Tests.ClipboardSetup;

namespace TerrainEditor.Desktop.Tests;

/// <summary>The copy of an Area selection (CopyData.Area.cs): how its objects' heights are kept.</summary>
public class CopyDataAreaTests
{
    // Trees and the like keep their height above the ground where each lands, as the Select tool's copies do: a tree
    // copied on a mound's top and pasted on flat ground stands on that ground (it floated at the mound's height above
    // the copy's average); a building piece keeps the building's shape, from the copy's average height.
    [Fact]
    public void TreesFollowTheGroundWhereTheyLandAndPiecesKeepTheShape()
    {
        var s = Walled();
        var c = CopyData.FromArea(Box(32, 32, 48, 48), s.Ground, s.Scene, [0, 1], n => n == Beech ? "Beech1" : "woodwall")!;
        Assert.Equal([true, false], c.Objects.Select(o => o.Follow));
        Assert.Equal(0, c.Objects[0].Dy, 2);
        var paste = new PasteTool { Clip = c, Ground = false, ClearSite = false };
        var (_, _, add) = paste.Apply(s.Ground, new Vector2(90, 90));
        Assert.Equal(H(s, 90, 90), add[0].Item1.Position.Y, 2);
        Assert.Equal(H(s, 90, 90) + H(s, 40, 40) + 1 - c.Middle.Y, add[1].Item1.Position.Y, 2);
    }

    // With the copied ground pasted too, a tree on it stands on the ground as the paste leaves it, and its ghost (drawn
    // before the click, from the ground as it is) shows it there.
    [Fact]
    public void ATreeOnTheCopiedGroundStandsOnItWhereItsGhostShows()
    {
        var s = Walled();
        var paste = new PasteTool { Clip = CopyData.FromArea(Box(32, 32, 48, 48), s.Ground, s.Scene, [0, 1], n => n == Beech ? "Beech1" : "woodwall"), ClearSite = false };
        var (ox, oz) = GridOrigin(s);
        var ghosts = paste.Ghosts(new Vector2(90, 90), g => H(s, (int)MathF.Round(g.X), (int)MathF.Round(g.Y)), ox, oz);
        var (_, _, add) = paste.Apply(s.Ground, new Vector2(90, 90));
        Assert.Equal(ghosts.Select(g => g.Position), add.Select(a => a.Item1.Position));
        Assert.Equal(s.Ground.HeightOf(90 * s.Ground.W + 90), add[0].Item1.Position.Y, 2);
        Assert.True(add[0].Item1.Position.Y > 31);
    }
}
