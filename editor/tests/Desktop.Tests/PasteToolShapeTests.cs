using System.Numerics;
using Xunit;
using static TerrainEditor.Desktop.Tests.ClipboardSetup;

namespace TerrainEditor.Desktop.Tests;

/// <summary>The ground as a paste leaves it (PasteTool.Shape.cs): where objects that follow it are shown before the click.</summary>
public class PasteToolShapeTests
{
    // With the Mask keeping the paste's ground off a spot, the tree's ghost stands where the paste leaves the tree: on
    // the ground as it was there, as the paste itself (the Mask refuses its ground there) puts it.
    [Fact]
    public void WithTheMaskTheGhostStandsWhereThePasteLeavesTheTree()
    {
        var s = Walled();
        var paste = new PasteTool { Clip = WalledCopy(s), ClearSite = false };
        float before = H(s, 90, 90);
        paste.MaskAt = (_, _) => 0;
        var ghosts = GhostsAt(paste, s, new Vector2(90, 90));
        var (_, _, add) = paste.Apply(s.Ground, new Vector2(90, 90), _ => 0);
        Assert.Equal(before, ghosts[0].Position.Y, 2);
        Assert.Equal(ghosts.Select(g => g.Position), add.Select(a => a.Item1.Position));
    }
}
