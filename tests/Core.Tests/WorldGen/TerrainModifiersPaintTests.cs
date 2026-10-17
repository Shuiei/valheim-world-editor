using System.Numerics;
using TerrainEditor.Terrain;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Painting a modifier's cleared ground into a zone's mask: the point takes the paint, the channels the paint sets, and a
/// paint the editor does not know is refused.
/// </summary>
public class TerrainModifiersPaintTests
{
    private const int N = 65;

    /// <summary>The offset of the point's vertex (zone 0, 0 at the origin) in the RGBA mask.</summary>
    private const int Centre = (32 * N + 32) * 4;

    [Fact]
    public void ANoPaintRadiusPaintsNothing()
    {
        float[] mask = new float[N * N * 4];

        TerrainModifiers.PaintCleared(0, 0, new float[N * N], mask, TerrainModifierSupport.Make(Vector3.Zero, paintCleared: true, paintRadius: 0));

        Assert.All(mask, v => Assert.Equal(0f, v));
    }

    [Fact]
    public void DirtPaintsRedAndGreenAndBlueButKeepsTheVegetation()
    {
        float[] mask = [.. Enumerable.Repeat(1f, N * N * 4)];
        TerrainModifiers.Modifier m = TerrainModifierSupport.Make(Vector3.Zero, paintCleared: true, paintRadius: 1, paint: TerrainModifiers.Paint.Dirt);

        TerrainModifiers.PaintCleared(0, 0, new float[N * N], mask, m);

        Assert.Equal(1f, mask[Centre]);
        Assert.Equal(0f, mask[Centre + 1]);
        Assert.Equal(1f, mask[Centre + 3]);
    }

    [Fact]
    public void ClearVegetationBlendsAllFourChannelsToNothing()
    {
        float[] mask = [.. Enumerable.Repeat(1f, N * N * 4)];
        TerrainModifiers.Modifier m = TerrainModifierSupport.Make(Vector3.Zero, paintCleared: true, paintRadius: 1, paint: TerrainModifiers.Paint.ClearVegetation);

        TerrainModifiers.PaintCleared(0, 0, new float[N * N], mask, m);

        Assert.Equal(0f, mask[Centre]);
        Assert.Equal(0f, mask[Centre + 3]);
    }

    [Fact]
    public void AnUnknownPaintIsRefused()
    {
        float[] mask = new float[N * N * 4];
        TerrainModifiers.Modifier m = TerrainModifierSupport.Make(Vector3.Zero, paintCleared: true, paintRadius: 1, paint: (TerrainModifiers.Paint)99);

        Assert.Throws<InvalidDataException>(() => TerrainModifiers.PaintCleared(0, 0, new float[N * N], mask, m));
    }
}
