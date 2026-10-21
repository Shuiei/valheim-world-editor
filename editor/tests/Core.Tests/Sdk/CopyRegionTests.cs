using System.Numerics;
using ValheimWorldEditor.Sdk;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>What a copy takes (CopyRegion): its middle and outline as given, a point inside as the editor's test says.</summary>
public class CopyRegionTests
{
    [Fact]
    public void APointIsInsideAsTheEditorSays()
    {
        Vector2[] outline = [new(0, 0), new(4, 0), new(4, 4)];
        var r = new CopyRegion(new Vector3(2, 0, 2), outline, (x, z) => x < 1 && z < 1);
        Assert.True(r.Contains(0.5f, 0.5f));
        Assert.False(r.Contains(3, 1));
        Assert.Equal(new Vector3(2, 0, 2), r.Middle);
        Assert.Same(outline, r.Outline);
    }
}
