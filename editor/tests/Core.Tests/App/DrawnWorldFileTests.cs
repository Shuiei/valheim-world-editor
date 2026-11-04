using DrawnWorlds;
using TerrainEditor.App;
using TerrainEditor.Save;
using Xunit;
using K = TerrainEditor.App.MasterMap.Kind;

namespace WorldEditor.Tests;

/// <summary>
/// A drawn world's file: the map kept in it (DrawnLand.Encode, as the game's DrawnWorlds mod reads it) comes back the
/// same, a world made from a map is marked with the editor's own generator and opens as that map, and a map kept by a
/// newer format or damaged is refused with why.
/// </summary>
public class DrawnWorldFileTests
{
    private static MasterMap Map()
    {
        var map = new MasterMap();
        map.Disc(1024, 1024, 300, K.Meadows);
        map.Disc(1200, 900, 60, K.Mountain);
        map.Start = (1030, 1020);
        return map;
    }

    [Fact]
    public void AKeptMapComesBackTheSame()
    {
        var map = Map();

        byte[] kept = DrawnLand.Encode(map.Cells, 1030, 1020);
        byte[] cells = DrawnLand.Decode(kept, out int column, out int row);

        Assert.Equal(map.Cells, cells);
        Assert.Equal((1030, 1020), (column, row));
        // Large areas of one kind shrink a lot.
        Assert.True(kept.Length < 64 * 1024, $"{kept.Length} bytes");
    }

    [Fact]
    public void AMapOfAnotherFormatOrDamagedIsRefused()
    {
        byte[] kept = DrawnLand.Encode(new MasterMap().Cells, -1, -1);
        byte[] newer = (byte[])kept.Clone();
        newer[0] = 99;
        byte[] cut = kept[..^8];

        Assert.Contains("format 99", Assert.Throws<InvalidDataException>(() => DrawnLand.Decode(newer, out _, out _)).Message);
        Assert.ThrowsAny<Exception>(() => DrawnLand.Decode(cut, out _, out _));
    }

    [Fact]
    public void AWorldMadeFromAMapOpensAsThatMap()
    {
        string dir = Path.Combine(Path.GetTempPath(), "vwe-drawn-" + Guid.NewGuid().ToString("N"));
        try
        {
            var map = Map();
            WorldCreator.Create(dir, "Drawn", "Seed1", rules: MasterMapWorlds.Rules(map));

            var save = WorldSave.Load(dir);

            var header = Assert.IsType<GeneratorHeader>(save.Generator);
            Assert.Equal(MasterMapWorlds.Id, header.Generator.Id);
            var read = Assert.IsType<MasterMap>(header.Settings.Value);
            Assert.Equal(map.Stamp(), read.Stamp());
            Assert.Equal(map.Start, read.Start);
            var rules = header.Generator.Rules(header.Settings, null);
            Assert.NotNull(rules.Land);
            Assert.Equal(map.StartPoint, rules.Start);
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, true);
            }
        }
    }
}
