using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// CrunchCodec's byte reader: the bytes of the range it was started on, in order, then zeros once the range is used up.
/// </summary>
public class CrunchCodecNextByteTests
{
    [Fact]
    public void TheBytesOfTheStartedRangeAreReadInOrder()
    {
        var codec = new CrunchCodec([9, 10, 11, 12, 13]);
        codec.Start(1, 2);

        Assert.Equal(10u, PrivateMember.Call<uint>(typeof(CrunchCodec), "NextByte", codec));
        Assert.Equal(11u, PrivateMember.Call<uint>(typeof(CrunchCodec), "NextByte", codec));
    }

    [Fact]
    public void PastTheEndOfTheRangeTheReaderGivesZeros()
    {
        var codec = new CrunchCodec([9, 10, 11, 12, 13]);
        codec.Start(1, 1);
        PrivateMember.Call<uint>(typeof(CrunchCodec), "NextByte", codec);

        Assert.Equal(0u, PrivateMember.Call<uint>(typeof(CrunchCodec), "NextByte", codec));
    }
}
