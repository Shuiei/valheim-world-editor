using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The flags an object is saved with: the base flags it keeps, and one for each part it has.
/// </summary>
public class ZdoDataSerializeFlagsTests
{
    [Fact]
    public void AnObjectWithNoPartsKeepsOnlyItsBaseFlags()
    {
        var z = new ZdoData { BaseFlags = 0x0100 };

        Assert.Equal(0x0100, z.SerializeFlags(small: false, rotated: false));
    }

    [Fact]
    public void ThePositionAndRotationFormsSetTheirFlags()
    {
        var z = new ZdoData();

        Assert.Equal(ZdoData.SmallPositionFlag, z.SerializeFlags(small: true, rotated: false));
        Assert.Equal(ZdoData.RotationFlag, z.SerializeFlags(small: false, rotated: true));
    }

    [Fact]
    public void EachPartTheObjectHasSetsItsFlag()
    {
        var z = new ZdoData { Connection = [1], BaseFlags = 0 };
        z.FloatList.Add((1, 2f));
        z.IntList.Add((3, 4));

        ushort flags = z.SerializeFlags(small: false, rotated: false);

        Assert.Equal(ZdoData.Connections | ZdoData.Floats | ZdoData.Ints, flags);
    }
}
