using System.Numerics;
using TerrainEditor.Save;
using TerrainEditor.Terrain;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The ground locations flatten, as overlays: the flat part and the edge it blends into (round, or square for a square
/// modifier), counted once even where it reaches into two zones of the area, and the editor's ground discs and players'
/// own changes left out.
/// </summary>
public class OverlaysFlattenTests
{
    /// <summary>A location placed at world x, z (its modifiers from the game's data, the tests' copy).</summary>
    private static PlacedObject Location(string name, float x, float z) => new(0, StableHash.Of(name), new Vector3(x, 30, z), Vector3.Zero, 0);

    [Fact]
    public void LocationsFlattenedGroundIsRingedOnceEach()
    {
        // A 2 × 2 zone area; the trader's fader on the zones' edge (in both), a goblin hut's square edge.
        var s = TestScenes.Flat(2).Scene;
        var modifiers = new TerrainModifiers([Location("FaderLocation", 32, 32), Location("GoblinHut02", 0, 60)]);
        var o = Overlays.Build(s, modifiers, _ => null);
        // The fader one, the hut two (a smaller square at its side).
        Assert.Equal(3, o.Flattened);
        // The fader levels and blends: both rings; the hut only blends, along a square.
        Assert.NotEmpty(o.Lines[Overlays.Layer.Flatten]);
        float[] edge = o.Lines[Overlays.Layer.FlattenEdge];
        Assert.NotEmpty(edge);
        // The hut's square: points 7 m from its middle along x or z (view space: x - 32, z mirrored).
        var hut = Enumerable.Range(0, edge.Length / 3).Select(i => new Vector2(edge[i * 3] + 32, -edge[i * 3 + 2] + 32))
            .Where(p => Vector2.Distance(p, new Vector2(0, 60)) < 9.5f).ToList();
        Assert.NotEmpty(hut);
        Assert.True(hut.Count(p => MathF.Abs(MathF.Max(MathF.Abs(p.X), MathF.Abs(p.Y - 60)) - 7) < 0.6f) >= 8);
        // None: nothing flattened.
        Assert.Equal(0, Overlays.Build(s, null, _ => null).Flattened);
    }
}
