using Avalonia.Headless.XUnit;
using WorldEditor.Tests;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The world map's build on the test world: one that fails is said (not left building for ever) and logged.
/// </summary>
[Collection("World files")]
public class MapViewTests
{
    [AvaloniaFact]
    public async Task AWorldMapThatCannotBeBuiltIsSaid()
    {
        string dir = Fixtures.CopyFixture();
        try
        {
            using var session = WorldSession.Open(dir);
            using var map = new MapView { BuildGlobal = _ => throw new InvalidDataException("a broken zone") };
            var said = new List<string>();
            map.Status += said.Add;
            var stdout = Console.Out;
            using var output = new StringWriter();
            try
            {
                Console.SetOut(TextWriter.Synchronized(output));
                map.Show(session);
                await TestWindows.Until(() => said.Count == 2);
            }
            finally
            {
                Console.SetOut(stdout);
            }
            Assert.Equal(["Building the world map (same as the game does)…", "Could not build the world map: a broken zone"], said);
            Assert.False(map.Ready);
            // The details go to the log.
            Assert.Contains("World map: could not build it: System.IO.InvalidDataException: a broken zone", output.ToString());
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(dir)!, recursive: true);
        }
    }
}
