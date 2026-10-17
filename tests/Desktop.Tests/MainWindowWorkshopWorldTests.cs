using Avalonia.Headless.XUnit;
using WorldEditor.Tests;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Into the Workshop from a world with unsaved changes (on a copy of the test world): asked first, Stay keeps the
/// world, Leave anyway closes it for the plot.
/// </summary>
[Collection("World files")]
public class MainWindowWorkshopWorldTests
{
    [AvaloniaFact]
    public async Task LeavingAWorldWithChangesForTheWorkshopAsksFirst()
    {
        string dir = Fixtures.CopyFixture();
        try
        {
            var w = await TestWindows.ChangedWorld(dir);
            var world = w.World;
            Assert.Equal(1, world!.Pending.Zones);
            var asked = new List<string>();
            bool answer = false;
            w.Ask = (title, _, _, _) => { asked.Add(title); return Task.FromResult(answer); };
            // Stay: the world and its change are kept.
            await w.OpenWorkshop(null);
            Assert.Equal("Leave the world", Assert.Single(asked));
            Assert.Same(world, w.World);
            Assert.False(w.InWorkshop);
            // Leave anyway: the world is closed, the plot opened.
            answer = true;
            await w.OpenWorkshop(null);
            Assert.Null(w.World);
            Assert.True(w.InWorkshop);
        }
        finally
        {
            string folder = Path.GetDirectoryName(dir)!;
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
    }
}
