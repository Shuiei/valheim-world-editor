using System.Numerics;
using Avalonia.Headless.XUnit;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

// Heightmaps: the area's ground out to a picture and back.
public class HeightmapTests
{
	private static float H(EditSession s, int x, int z) => s.Scene.Heights[z * s.Scene.W + x];

	[AvaloniaFact]
	public void AnExportedAreaComesBackAsItWas()
	{
		string dir = Path.Combine(Path.GetTempPath(), "vwe-hm-" + Guid.NewGuid().ToString("N")[..8]);
		try
		{
			var w = new MainWindow(load: false) { Width = 1600, Height = 1000 };
			w.Show();
			var s = EditTests.Flat(2);
			w.View.Show(s.Scene, null);
			w.Edit(s);
			s.Shape(64, 64, Formula.Compile("h * smooth(1 - d / r)", new[] { "d", "r", "h" }), 20, 6, "hill");
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
			var s = EditTests.Flat(2);
			w.View.Show(s.Scene, null);
			w.Edit(s);
			s.Shape(64, 64, Formula.Compile("x > 0 ? 4 : 0", new[] { "x" }), 60, 0, "step");
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
}
