using Avalonia.Headless.XUnit;
using WorldEditor.Tests;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The world map's build on the test world: one that fails is said (not left building for ever).
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
            map.Show(session);
            await TestWindows.Until(() => said.Count == 2);
            Assert.Equal(["Building the world map (same as the game does)…", "Could not build the world map: a broken zone"], said);
            Assert.False(map.Ready);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(dir)!, recursive: true);
        }
    }
}
