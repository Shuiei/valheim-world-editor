using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The checks of a crn header called on their own (CrunchHeader.Validation): the header size's range, the CRCs over the
/// header and the data, and the texture format. The header is read from a fixture; each check is then given the bytes
/// it looks at, changed or not.
/// </summary>
public class CrunchHeaderValidationTests
{
    /// <summary>The crn fixture the checks are made on.</summary>
    private static byte[] Fixture() => CrnFiles.Fixture("crunch-dxt5.crn");

    /// <summary>The header size the file gives (its bytes 2 and 3).</summary>
    private static int HeaderSize(byte[] crn) => (crn[2] << 8) | crn[3];

    [Fact]
    public void AHeaderSizeOfTheFileIsWithinTheSizeChecks()
    {
        byte[] crn = Fixture();
        var header = new CrunchHeader(crn);

        PrivateCall.Instance(header, "CheckSizes", crn, HeaderSize(crn));
    }

    [Fact]
    public void AHeaderSizeBeyondTheFileIsRefusedByTheSizeCheck()
    {
        byte[] crn = Fixture();
        var header = new CrunchHeader(crn);

        var e = Assert.Throws<InvalidDataException>(() => PrivateCall.Instance(header, "CheckSizes", crn, crn.Length + 1));
        Assert.Equal("crn sizes out of the data", e.Message);
    }

    [Fact]
    public void AHeaderSizeTooSmallForTheFixedPartAndTheLevelsIsRefusedByTheSizeCheck()
    {
        byte[] crn = Fixture();
        var header = new CrunchHeader(crn);

        var e = Assert.Throws<InvalidDataException>(() => PrivateCall.Instance(header, "CheckSizes", crn, 0));
        Assert.Equal("crn sizes out of the data", e.Message);
    }

    [Fact]
    public void TheChecksumsOfAnUnchangedFileAreRight()
    {
        byte[] crn = Fixture();
        var header = new CrunchHeader(crn);

        PrivateCall.Instance(header, "CheckChecksums", crn, HeaderSize(crn));
    }

    /// <summary>The checksum check's refusal of the fixture with one of its bytes changed.</summary>
    private static InvalidDataException ChecksumRefusal(Action<byte[]> change)
    {
        byte[] crn = Fixture();
        var header = new CrunchHeader(crn);
        byte[] changed = CrnFiles.Copy(crn, change);
        return Assert.Throws<InvalidDataException>(() => PrivateCall.Instance(header, "CheckChecksums", changed, HeaderSize(crn)));
    }

    [Fact]
    public void AChangedHeaderByteFailsTheHeaderChecksum()
    {
        Assert.Equal("crn CRC check failed", ChecksumRefusal(c => c[13] ^= 1).Message);
    }

    [Fact]
    public void AChangedDataByteFailsTheDataChecksum()
    {
        Assert.Equal("crn CRC check failed", ChecksumRefusal(c => c[^1] ^= 1).Message);
    }

    [Fact]
    public void ASingleFaceDxt5TextureIsAcceptedByTheFormatCheck()
    {
        var header = new CrunchHeader(Fixture());

        PrivateCall.Instance(header, "CheckFormat", 1);
    }

    [Fact]
    public void AFaceCountOtherThanOneIsRefusedByTheFormatCheck()
    {
        var header = new CrunchHeader(Fixture());

        var e = Assert.Throws<InvalidDataException>(() => PrivateCall.Instance(header, "CheckFormat", 6));
        Assert.Equal("crn texture not read here", e.Message);
    }
}
