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
    [Fact]
    public void EveryLevelUnpacksToTheReferenceBlocks()
    {
        foreach (var (name, format, size) in new[] { ("crunch-dxt1", 10, (64, 64, 7)), ("crunch-dxt5", 12, (48, 20, 6)) })
        {
            byte[] crn = CrnFiles.Fixture(name + ".crn"), dds = CrnFiles.Fixture(name + ".dds");
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
    public void ALevelPickedFromTheHeaderComesWithIt()
    {
        byte[] crn = CrnFiles.Fixture("crunch-dxt5.crn");
        var (info, level, blocks) = Crunch.Unpack(crn, t => t.Levels - 1)!.Value;
        Assert.Equal(Crunch.Read(crn), info);
        Assert.Equal(5, level);
        Assert.Equal(Crunch.Unpack(crn, 5), blocks);
        Assert.Null(Crunch.Unpack(crn, t => t.Levels));
    }

    [Fact]
    public void BrokenFilesGiveNull()
    {
        byte[] crn = CrnFiles.Fixture("crunch-dxt5.crn");
        byte[] garbage = new byte[crn.Length];
        new Random(5).NextBytes(garbage);
        byte[] header = CrnFiles.Copy(crn, c => c[13] ^= 1);
        byte[] data = CrnFiles.Copy(crn, c => c[^1] ^= 1);
        byte[] dxn = CrnFiles.Copy(crn, c => c[18] = 7);
        byte[] cube = CrnFiles.Copy(crn, c => c[17] = 6);
        foreach (byte[] broken in new[] { [], garbage, crn[..(crn.Length / 2)], crn[..40], header, data, CrnFiles.Recheck(dxn), CrnFiles.Recheck(cube) })
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
            byte[] crn = CrnFiles.Fixture(name);
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
                    CrnFiles.Recheck(changed);
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
