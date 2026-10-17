using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The composite readers: vectors, small rotations and item counts, as Valheim's package writes them.
/// </summary>
public class ValheimReaderCompositeTests
{
    [Fact]
    public void ASmallRotationAroundYIsTwoBytesOfHalfDegrees()
    {
        // 0x8064: the high bit marks a pure Y turn, and 100 half degrees is 50 degrees.
        using var r = new ValheimReader(new MemoryStream([0x64, 0x80]));

        Assert.Equal(new System.Numerics.Vector3(0, 50, 0), r.ReadSmallRotation());
    }

    [Fact]
    public void AnItemCountBeforeVersionThirtyThreeIsOneByte()
    {
        using var r = new ValheimReader(new MemoryStream([5]));

        Assert.Equal(5, r.ReadNumItems(32));
    }

    [Fact]
    public void AnItemCountFromVersionThirtyThreeCanTakeTwoBytes()
    {
        using var r = new ValheimReader(new MemoryStream([0x81, 0x02]));

        Assert.Equal(258, r.ReadNumItems(41));
    }

    [Fact]
    public void AVectorIsThreeLittleEndianFloats()
    {
        byte[] bytes = new byte[12];
        BitConverter.GetBytes(1f).CopyTo(bytes, 0);
        BitConverter.GetBytes(2f).CopyTo(bytes, 4);
        BitConverter.GetBytes(3f).CopyTo(bytes, 8);
        using var r = new ValheimReader(new MemoryStream(bytes));

        Assert.Equal(new System.Numerics.Vector3(1, 2, 3), r.ReadVector3());
    }

    [Fact]
    public void TwoShortsAreASectorsXAndY()
    {
        using var r = new ValheimReader(new MemoryStream([1, 0, 2, 0]));

        Assert.Equal(((short)1, (short)2), r.ReadVector2s());
    }
}
