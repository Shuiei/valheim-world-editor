using System.Numerics;
using Avalonia.Headless.XUnit;
using WorldEditor.Tests;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The world map's build on the test world: one that fails is said (not left building for ever) and logged. The pins'
/// shapes are made again only when what they show changes.
/// </summary>
[Collection("World files")]
public class MapViewTests
{
    /// <summary>
    /// What the map says when it shows the test world and its build fails ("a broken zone"): the building status, then
    /// the failure. <paramref name="check"/> looks at the map once both are said.
    /// </summary>
    internal static async Task<List<string>> FailedBuildSays(Action<MapView>? check = null)
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
            check?.Invoke(map);
            return said;
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(dir)!, recursive: true);
        }
    }

    [AvaloniaFact]
    public async Task AWorldMapThatCannotBeBuiltIsSaid()
    {
        var stdout = Console.Out;
        using var output = new StringWriter();
        List<string> said;
        try
        {
            Console.SetOut(TextWriter.Synchronized(output));
            said = await FailedBuildSays(map => Assert.False(map.Ready));
        }
        finally
        {
            Console.SetOut(stdout);
        }
        Assert.Equal(["Building the world map (same as the game does)…", "Could not build the world map: a broken zone"], said);
        // The details go to the log.
        Assert.Contains("World map: could not build it: System.IO.InvalidDataException: a broken zone", output.ToString());
    }

    [AvaloniaFact]
    public void PinShapesAreMadeAgainOnlyWhenWhatTheyShowChanges()
    {
        List<Vector2> pins = [new(10, 20), new(30, 40)];
        using var map = new MapView { MetersPerPixel = 2, Pins = pins };
        float[] shapes = map.PinShapes();
        // Two discs of 8 triangles a pin, 6 floats a corner.
        Assert.Equal(2 * 2 * 8 * 3 * 6, shapes.Length);
        // The first pin's first triangle, dark: its middle, then its outer disc's edge (4.5 px, 9 m) at 0° and 45°.
        Assert.Equal([10f, 20f, 30 / 255f, 15 / 255f, 5 / 255f, 0.9f, 19f, 20f], shapes[..8]);
        Assert.Equal(10 + 9 * MathF.Cos(MathF.Tau / 8), shapes[12], 4);
        Assert.Equal(20 + 9 * MathF.Sin(MathF.Tau / 8), shapes[13], 4);
        // Frames with nothing changed: the same shapes.
        Assert.Same(shapes, map.PinShapes());
        // The list given was copied: changing it afterwards changes nothing shown.
        pins[0] = new Vector2(50, 50);
        Assert.Equal(new Vector2(10, 20), map.Pins[0]);
        Assert.Same(shapes, map.PinShapes());
        // A pin chosen: its ring and larger discs instead.
        map.PinChosen = 1;
        float[] chosen = map.PinShapes();
        Assert.Equal((2 * 8 * 3 + 24 * 6 + 2 * 12 * 3) * 6, chosen.Length);
        Assert.Same(chosen, map.PinShapes());
        // Zoomed: made again.
        map.MetersPerPixel = 4;
        float[] zoomed = map.PinShapes();
        Assert.NotSame(chosen, zoomed);
        Assert.Equal(chosen.Length, zoomed.Length);
        // A player online: made again with them.
        List<(string, float, float)> players = [("Ragnar", 0, 0)];
        map.Players = players;
        float[] withPlayer = map.PinShapes();
        Assert.Equal(zoomed.Length + 2 * 12 * 3 * 6, withPlayer.Length);
        // The list given was copied: changing it afterwards changes nothing shown.
        players[0] = ("Ragnar", 50, 50);
        Assert.Equal((0f, 0f), (map.Players[0].X, map.Players[0].Z));
        Assert.Same(withPlayer, map.PinShapes());
        // New search results (a new list): made again.
        map.Pins = [];
        map.PinChosen = -1;
        Assert.Equal(2 * 12 * 3 * 6, map.PinShapes().Length);
    }
}
