using System.Numerics;
using Avalonia.Headless.XUnit;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Heightmaps: the area's ground out to a picture and back, and a picture's values put into the ground (PutHeights).
/// </summary>
public class AreaPanelHeightmapTests
{
    /// <summary>
    /// The height at grid point (x, z) of the area.
    /// </summary>
    private static float H(EditSession s, int x, int z) => s.Scene.Heights[z * s.Scene.W + x];

    [AvaloniaFact]
    public void AnExportedAreaComesBackAsItWas()
    {
        string dir = Path.Combine(Path.GetTempPath(), "vwe-hm-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var w = new MainWindow(load: false) { Width = 1600, Height = 1000 };
            w.Show();
            var s = TestScenes.Flat(2);
            w.View.Show(s.Scene, null);
            w.Edit(s);
            s.Shape(64, 64, Formula.Compile("h * smooth(1 - d / r)", ["d", "r", "h"]), 20, 6, "hill");
            float top = H(s, 64, 64);
            var p = w.AreaPanel;
            string said = p.ExportHeightmap(dir)!;
            Assert.StartsWith("Heightmap written to", said);
            string file = Directory.GetFiles(dir).Single();
            // The hill goes away, then the picture brings it back (the whole area: nothing is selected).
            s.Undo();
            Assert.Equal(30, H(s, 64, 64), 3);
            p.LoadPicture(file);
            Assert.Equal(30, (double)p.LowestBox.Value!, 2);
            Assert.Equal(top, (double)p.HighestBox.Value!, 2);
            Assert.Contains("with the heights it was exported with", p.PictureInfo.Text);
            p.PutPicture();
            Assert.Equal(top, H(s, 64, 64), 2);
            Assert.Equal("Heightmap import", s.UndoLabel);
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    [AvaloniaFact]
    public void APictureFitsTheSelectionWithTheChosenHeights()
    {
        string dir = Path.Combine(Path.GetTempPath(), "vwe-hm-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var w = new MainWindow(load: false) { Width = 1600, Height = 1000 };
            w.Show();
            var s = TestScenes.Flat(2);
            w.View.Show(s.Scene, null);
            w.Edit(s);
            s.Shape(64, 64, Formula.Compile("x > 0 ? 4 : 0", ["x"]), 60, 0, "step");
            var p = w.AreaPanel;
            p.ExportHeightmap(dir);
            s.Undo();
            var a = w.View.Area;
            a.Soft = 0;
            a.Points.Add(new Vector2(20, 20));
            a.Points.Add(new Vector2(100, 100));
            p.LoadPicture(Directory.GetFiles(dir).Single());
            p.LowestBox.Value = 31;
            p.HighestBox.Value = 33;
            p.PutPicture();
            // Inside the selection: black is 31, white 33; outside it nothing changed.
            Assert.Equal(31, H(s, 30, 60), 1);
            Assert.Equal(33, H(s, 90, 60), 1);
            Assert.Equal(30, H(s, 110, 60), 3);
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    // PutHeights: black at lo, white at hi, faded by each point's weight; points outside the picture left alone; the
    // game's ±8 m holds, and fully weighted points it stops are counted.
    [Fact]
    public void APicturesValuesGoIntoTheGroundFadedByWeight()
    {
        var g = TestScenes.Flat(1).Ground;
        int Pt(int x) => 10 * g.W + x;
        var (touched, clamped) = AreaPanel.PutHeights(g, [0, 1, 1], 10, 10, 3, 1, [(Pt(10), 1), (Pt(11), 0.25f), (Pt(12), 1), (Pt(13), 1)], 30, 50);
        Assert.Equal([Pt(10), Pt(11), Pt(12)], touched);
        Assert.Equal(1, clamped);
        Assert.Equal(30, g.HeightOf(Pt(10)), 3);
        Assert.Equal(35, g.HeightOf(Pt(11)), 3);
        Assert.Equal(38, g.HeightOf(Pt(12)), 3);
        Assert.Equal(30, g.HeightOf(Pt(13)), 3);
    }
}
