using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Crunched textures unpacked to DXT blocks (Crunch), on images made for the tests (tests/fixtures/crunch-*.crn: a 64 x
/// 64 colour checker crunched to DXT1, a 48 x 20 gradient with an alpha ring crunched to DXT5, both by DaemonEngine's
/// crunch tool in Unity's format; crunch-*.dds: the same tool's unpacking of them, every level). Broken files are
/// said rather than unpacked wrong, and no file makes the decoder throw.
/// </summary>
public class CrunchTests
{
    /// <summary>A fixture's bytes.</summary>
    private static byte[] Fixture(string name) => File.ReadAllBytes(Path.Combine(Fixtures.Root, name));

    /// <summary>
    /// crunch's CRC-16 (a copy of the decoder's, so that the tests can change a file's header or data and keep its
    /// checks right).
    /// </summary>
    private static ushort Crc16(ReadOnlySpan<byte> data)
    {
        ushort crc = 0xFFFF;
        foreach (byte b in data)
        {
            ushort q = (ushort)(b ^ (crc >> 8));
            crc <<= 8;
            ushort r = (ushort)((q >> 4) ^ q);
            crc ^= r;
            r <<= 5;
            crc ^= r;
            r <<= 7;
            crc ^= r;
        }
        return (ushort)~crc;
    }

    /// <summary>The file with its data's and header's CRCs made right again (when its sizes are inside it).</summary>
    private static byte[] Recheck(byte[] crn)
    {
        int headerSize = (crn[2] << 8) | crn[3];
        long dataSize = ((long)crn[6] << 24) | ((long)crn[7] << 16) | ((long)crn[8] << 8) | crn[9];
        if (headerSize < 12 || dataSize < headerSize || dataSize > crn.Length)
        {
            return crn;
        }
        ushort data = Crc16(crn.AsSpan(headerSize, (int)dataSize - headerSize));
        crn[10] = (byte)(data >> 8);
        crn[11] = (byte)data;
        ushort header = Crc16(crn.AsSpan(6, headerSize - 6));
        crn[4] = (byte)(header >> 8);
        crn[5] = (byte)header;
        return crn;
    }

    [Fact]
    public void EveryLevelUnpacksToTheReferenceBlocks()
    {
        foreach (var (name, format, size) in new[] { ("crunch-dxt1", 10, (64, 64, 7)), ("crunch-dxt5", 12, (48, 20, 6)) })
        {
            byte[] crn = Fixture(name + ".crn"), dds = Fixture(name + ".dds");
            var info = Crunch.Read(crn)!;
            Assert.Equal(size, (info.Width, info.Height, info.Levels));
            Assert.Equal(format == 12, info.Dxt5);
            // The DDS file: a 128-byte header then each level's blocks.
            int at = 128, w = info.Width, h = info.Height;
            for (int level = 0; level < info.Levels; level++)
            {
                int bytes = GameBundles.LevelSize(format, w, h);
                Assert.Equal(dds.AsSpan(at, bytes).ToArray(), Crunch.Unpack(crn, level));
                at += bytes;
                w = Math.Max(1, w / 2);
                h = Math.Max(1, h / 2);
            }
            Assert.Equal(dds.Length, at);
            Assert.Null(Crunch.Unpack(crn, -1));
            Assert.Null(Crunch.Unpack(crn, info.Levels));
        }
    }

    [Fact]
    public void BrokenFilesGiveNull()
    {
        byte[] crn = Fixture("crunch-dxt5.crn");
        byte[] garbage = new byte[crn.Length];
        new Random(5).NextBytes(garbage);
        byte[] header = (byte[])crn.Clone();
        header[13] ^= 1;
        byte[] data = (byte[])crn.Clone();
        data[^1] ^= 1;
        byte[] dxn = (byte[])crn.Clone();
        dxn[18] = 7;
        byte[] cube = (byte[])crn.Clone();
        cube[17] = 6;
        foreach (byte[] broken in new[] { [], garbage, crn[..(crn.Length / 2)], crn[..40], header, data, Recheck(dxn), Recheck(cube) })
        {
            Assert.Null(Crunch.Read(broken));
            Assert.Null(Crunch.Unpack(broken, 0));
        }
    }

    [Fact]
    public void NoChangedByteMakesTheDecoderThrow()
    {
        // Every byte changed, its checks made right (as a made-up file would have them): unpacked or null, whole blocks.
        foreach (string name in new[] { "crunch-dxt1.crn", "crunch-dxt5.crn" })
        {
            byte[] crn = Fixture(name);
            var info = Crunch.Read(crn)!;
            for (int i = 6; i < crn.Length; i++)
            {
                if (i is 10 or 11)
                {
                    continue;
                }
                foreach (byte change in new byte[] { 1, 0x80, 0xFF })
                {
                    byte[] changed = (byte[])crn.Clone();
                    changed[i] ^= change;
                    Recheck(changed);
                    for (int level = 0; level < info.Levels; level++)
                    {
                        if (Crunch.Unpack(changed, level) is { } blocks)
                        {
                            Assert.Equal(0, blocks.Length % (info.Dxt5 ? 16 : 8));
                        }
                    }
                }
            }
        }
    }
}
