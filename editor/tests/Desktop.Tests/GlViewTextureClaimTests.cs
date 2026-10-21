using Avalonia.Headless.XUnit;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The textures a model loader claims to read: one loader claims a texture, so no other reads it too, and another
/// texture is claimed on its own.
/// </summary>
public class GlViewTextureClaimTests
{
    [AvaloniaFact]
    public void ATextureIsClaimedByOneLoaderOnly()
    {
        var v = new GlView();
        Assert.True(v.ClaimTexture("tree.png"));
        Assert.False(v.ClaimTexture("tree.png"));
        Assert.True(v.ClaimTexture("rock.png"));
        string[] claimed = [.. v.TexturesClaimed.Order()];
        Assert.Equal(["rock.png", "tree.png"], claimed);
    }
}
