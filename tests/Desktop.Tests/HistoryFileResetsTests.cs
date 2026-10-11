using Avalonia.Headless.XUnit;
using TerrainEditor.Editing;
using WorldEditor.Tests;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The history on disk with a zone reset among its steps (written and read back with its zone and options), and a
/// world folder whose save is gone having no stamp to compare. On a copy of the test world.
/// </summary>
[Collection("World files")]
public class HistoryFileResetsTests
{
    [AvaloniaFact]
    public void AZoneResetStepComesBackWhenTheWorldIsOpenedAgain()
    {
        string dir = Fixtures.CopyFixture();
        try
        {
            var scene = WorldScene.Load(dir, 0, 0, 1);
            var s = scene.Session!;
            var reset = new ZoneReset(0, 0, KeepBuildings: true, Ground: false);
            s.Commit("Reset zone", null, [], [], [(reset, false, true)]);
            Assert.Equal("1 zone reset", s.UndoList[^1].Describe());
            Assert.True(s.Save().Saved);
            scene.Owner!.Dispose();
            var again = WorldScene.Load(dir, 0, 0, 1);
            var step = Assert.Single(again.Session!.UndoList);
            Assert.Equal("Reset zone", step.Label);
            Assert.Equal([(reset, false, true)], step.Resets);
            again.Owner!.Dispose();
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(dir)!, recursive: true);
        }
    }

    [AvaloniaFact]
    public void AWorldWhoseSaveIsGoneHasNoStamp()
    {
        string dir = Fixtures.CopyFixture();
        try
        {
            using var w = WorldSession.Open(dir);
            Assert.NotNull(HistoryFile.Stamp(w));
            foreach (string f in Directory.GetFiles(dir, "_main.*.chunks"))
            {
                File.Delete(f);
            }
            Assert.Null(HistoryFile.Stamp(w));
            // A folder that is gone: none either.
            Directory.Delete(dir, recursive: true);
            Assert.Null(HistoryFile.Stamp(w));
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(dir)!, recursive: true);
        }
    }
}
