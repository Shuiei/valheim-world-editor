using Avalonia.Headless.XUnit;
using WorldEditor.Tests;
using Xunit;
using static TerrainEditor.Desktop.Tests.HistoryFileSetup;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// A history file that cannot be written, damaged, or claims more objects than it holds: the editor still opens the world.
/// </summary>
[Collection("World files")]
public class HistoryFileTests
{
    // The history file cannot be put in place (a folder stands at its path): the save is still done, and no
    // half-written ".new" file is left beside it.
    [AvaloniaFact]
    public void AHistoryThatCannotBeWrittenLeavesNoTemporaryFile()
    {
        string dir = Fixtures.CopyFixture();
        string? path = null;
        try
        {
            var scene = WorldScene.Load(dir, 0, 0, 1);
            var s = scene.Session!;
            path = HistoryFile.PathOf(HistoryFile.KeyOf(scene.Owner!));
            Directory.CreateDirectory(Path.Combine(path, "in the way"));
            s.Shape(32, 32, Formula.Compile("3", []), 4, 0, "Raise");
            Assert.True(s.Save().Saved);
            Assert.False(File.Exists(path + ".new"));
            scene.Owner!.Dispose();
        }
        finally
        {
            if (path != null && Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
            Done(dir);
        }
    }

    // A history file damaged after its header (a count out of range, as a file changed by hand can
    // have): the world still opens, without it (it threw, and the world could not be opened).
    [AvaloniaFact]
    public void ADamagedHistoryFileDoesNotStopTheWorldOpening()
    {
        string dir = Fixtures.CopyFixture();
        try
        {
            EditAndSave(dir);
            var probe = WorldSession.Open(dir);
            string path = HistoryFile.PathOf(HistoryFile.KeyOf(probe));
            probe.Dispose();
            byte[] raw = Unzip(path);
            BitConverter.GetBytes(-5).CopyTo(raw, CountOffset(raw));
            Zip(path, raw);
            using var world = WorldSession.Open(dir);
            Assert.Null(world.Restored);
        }
        finally
        {
            Done(dir);
        }
    }

    // A count far past what the file holds (200 million objects) is refused at the file's end, without making room
    // for that many first (it allocated gigabytes, and relied on running out of memory).
    [AvaloniaFact]
    public void AHugeCountInAHistoryFileIsNotAllocatedBeforeItIsRead()
    {
        string dir = Fixtures.CopyFixture();
        try
        {
            EditAndSave(dir);
            using var world = WorldSession.Open(dir);
            string path = HistoryFile.PathOf(HistoryFile.KeyOf(world));
            byte[] raw = Unzip(path);
            BitConverter.GetBytes(200_000_000).CopyTo(raw, CountOffset(raw));
            Zip(path, raw);
            long before = GC.GetAllocatedBytesForCurrentThread();
            Assert.Null(HistoryFile.Read(world));
            Assert.InRange(GC.GetAllocatedBytesForCurrentThread() - before, 0, 64 << 20);
        }
        finally
        {
            Done(dir);
        }
    }
}
