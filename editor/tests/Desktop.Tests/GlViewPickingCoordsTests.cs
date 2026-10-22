using System.Numerics;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The ground's height at grid points (GlView.Picking.Coords), which the tools' marks are drawn at.
/// </summary>
public class GlViewPickingCoordsTests
{
    // A mark past the area's edge (a Path curve bulging out, the ring of a brush at the edge) is drawn at the edge's
    // height: it was put 1000 m down, and drawn as a spike across the view.
    [AvaloniaFact]
    public void PastTheEdgeTheGroundIsTheEdgesHeight()
    {
        var area = TestScenes.Flat(1);
        var v = new GlView();
        var w = new Window { Width = 400, Height = 300, Content = v };
        w.Show();
        try
        {
            v.Show(area.Scene, null);
            Assert.Equal(30, v.GridHeight(new Vector2(10, 10)), 3);
            Assert.Equal(30, v.GridHeight(new Vector2(-3, 10)), 3);
            Assert.Equal(30, v.GridHeight(new Vector2(area.Scene.W + 2, -4)), 3);
        }
        finally
        {
            w.Close();
        }
    }
}
