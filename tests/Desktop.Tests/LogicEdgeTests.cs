using System.Numerics;
using TerrainEditor.Save;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

// Edge cases of the editor's small rules: every formula function and comparison, every Mask paint
// rule and its warnings, stamps and the Place tool's memory when their files are broken or cannot be
// written, the game's biome colours, finding a world by folder or name, and the overlays' markers,
// square rings and styles.
public class LogicEdgeTests
{
	private static double Eval(string text, double x = 0, double z = 0)
	{
		var f = Formula.Compile(text, new[] { "x", "z" });
		var env = new Formula.Env();
		env.Vars["x"] = x;
		env.Vars["z"] = z;
		return f(env);
	}

	[Theory]
	[InlineData("tan(0)", 0)]
	[InlineData("abs(-3)", 3)]
	[InlineData("sqrt(16)", 4)]
	[InlineData("sqrt(-4)", 0)]
	[InlineData("exp(0)", 1)]
	[InlineData("log(1)", 0)]
	[InlineData("floor(2.7)", 2)]
	[InlineData("ceil(2.2)", 3)]
	[InlineData("min(4, 2, 9)", 2)]
	[InlineData("max(4, 2, 9)", 9)]
	[InlineData("pow(2, 10)", 1024)]
	[InlineData("atan2(0, 1)", 0)]
	[InlineData("sign(-5)", -1)]
	[InlineData("bell(0)", 1)]
	[InlineData("pi", Math.PI)]
	[InlineData("2 <= 2", 1)]
	[InlineData("3 <= 2", 0)]
	[InlineData("2 >= 3", 0)]
	[InlineData("3 >= 3", 1)]
	[InlineData("2 == 2", 1)]
	[InlineData("2 != 2", 0)]
	[InlineData("2 != 3", 1)]
	public void EveryFunctionAndComparison(string text, double expected) => Assert.Equal(expected, Eval(text), 6);

	[Fact]
	public void LogOfZeroAndEmptyMinMaxStayFinite()
	{
		Assert.True(double.IsFinite(Eval("log(0)")));
		Assert.Equal(double.PositiveInfinity, Eval("min()"));
		Assert.Equal(double.NegativeInfinity, Eval("max()"));
	}

	private static Ground PaintedGround(out int dirt, out int fields, out int paved, out int faint, out int bare)
	{
		var g = new Ground(65, 65, 0, 0, 1);
		Array.Fill(g.Base, 30f);
		void Paint(int p, float r, float gr, float b)
		{
			g.PMod[p] = 1;
			g.Paint[p * 4] = r;
			g.Paint[p * 4 + 1] = gr;
			g.Paint[p * 4 + 2] = b;
			g.Paint[p * 4 + 3] = 1;
		}
		(dirt, fields, paved, faint, bare) = (10, 20, 30, 40, 50);
		Paint(dirt, 1, 0, 0);
		Paint(fields, 0, 1, 0);
		Paint(paved, 0, 0, 1);
		Paint(faint, 0.05f, 0.05f, 0.05f);
		return g;
	}

	[Fact]
	public void EveryPaintRuleOfTheMask()
	{
		var g = PaintedGround(out int dirt, out int fields, out int paved, out int faint, out int bare);
		var biomes = new int[65 * 65];
		bool[] Pass(Mask.PaintRule rule)
		{
			var f = new Mask { On = true, Paint = rule }.For(g, biomes)!;
			return new[] { dirt, fields, paved, faint, bare }.Select(p => f(p) > 0).ToArray();
		}
		Assert.Equal(new[] { true, true, true, true, true }, Pass(Mask.PaintRule.Any));
		Assert.Equal(new[] { false, false, false, true, true }, Pass(Mask.PaintRule.Unpainted));
		Assert.Equal(new[] { true, true, true, false, false }, Pass(Mask.PaintRule.Painted));
		Assert.Equal(new[] { true, false, false, false, false }, Pass(Mask.PaintRule.Dirt));
		Assert.Equal(new[] { false, true, false, false, false }, Pass(Mask.PaintRule.Cultivated));
		Assert.Equal(new[] { false, false, true, false, false }, Pass(Mask.PaintRule.Paved));
	}

	[Fact]
	public void TheMaskWarnsAboutRangesThatMatchNothing()
	{
		var m = new Mask { On = true, HeightMin = 50, HeightMax = 10, SlopeMin = 40, SlopeMax = 5 };
		string w = m.Warning();
		Assert.Contains("Height min is above max", w);
		Assert.Contains("Slope min is above max", w);
		Assert.Equal("", new Mask { On = true, HeightMin = 10, HeightMax = 50 }.Warning());
	}

	[Fact]
	public void APictureThatIsNotOneMakesNoStamp()
	{
		Assert.Null(Stamps.FromPicture(new byte[] { 1, 2, 3, 4 }));
		Assert.Null(Stamps.FromPicture(Array.Empty<byte>()));
	}

	[Fact]
	public void BrokenOrUnwritableFilesDoNotStopStampsAndPlaceMemory()
	{
		string dir = Path.Combine(Path.GetTempPath(), "vwe-broken-" + Guid.NewGuid().ToString("N")[..8]);
		Directory.CreateDirectory(dir);
		string? stamps = Stamps.PathOverride, place = PlaceMemory.PathOverride;
		try
		{
			Stamps.PathOverride = Path.Combine(dir, "stamps.json");
			PlaceMemory.PathOverride = Path.Combine(dir, "place.json");
			File.WriteAllText(Stamps.PathOverride, "{ not json");
			File.WriteAllText(PlaceMemory.PathOverride, "[[[");
			Assert.Empty(Stamps.LoadKept());
			// Entries without their weights (it threw: the editor did not start).
			File.WriteAllText(Stamps.PathOverride, "[null, {\"Name\": \"pic:1\", \"Label\": \"x\", \"Weights\": null}]");
			Assert.Empty(Stamps.LoadKept());
			var memory = PlaceMemory.Load();
			Assert.Empty(memory.Recent);
			// Written into a folder that cannot hold the file: nothing thrown.
			Stamps.PathOverride = Path.Combine(dir, "missing", "stamps.json");
			PlaceMemory.PathOverride = Path.Combine(dir, "missing", "place.json");
			Stamps.SaveKept(new[] { new Stamps.Stamp("pic:1", "x", new float[Stamps.Size * Stamps.Size], Loaded: true) });
			memory.Save();
		}
		finally
		{
			Stamps.PathOverride = stamps;
			PlaceMemory.PathOverride = place;
			Directory.Delete(dir, true);
		}
	}

	[Theory]
	[InlineData(1, new byte[] { 0, 0, 0, 0 })]
	[InlineData(2, new byte[] { 255, 0, 0, 0 })]
	[InlineData(4, new byte[] { 0, 255, 0, 0 })]
	[InlineData(8, new byte[] { 0, 0, 255, 0 })]
	[InlineData(16, new byte[] { 0, 0, 0, 255 })]
	[InlineData(32, new byte[] { 255, 0, 0, 255 })]
	[InlineData(64, new byte[] { 0, 255, 0, 0 })]
	[InlineData(256, new byte[] { 0, 0, 0, 0 })]
	[InlineData(512, new byte[] { 0, 0, 255, 255 })]
	public void EachBiomeHasTheGamesColour(int biome, byte[] rgba) => Assert.Equal(rgba, WorldScene.BiomeRgba(biome));

	[Fact]
	public void AWorldIsFoundByItsFolderOrSaysWhyNot()
	{
		string dir = EditTests.CopyFixture();
		try
		{
			Assert.Equal(Path.GetFullPath(dir), WorldScene.FindWorld(dir));
			var ex = Assert.Throws<InvalidOperationException>(() => WorldScene.FindWorld("No World By This Name 123"));
			Assert.Equal("No world called No World By This Name 123.", ex.Message);
		}
		finally
		{
			Directory.Delete(Path.GetDirectoryName(dir)!, true);
		}
	}

	[Fact]
	public void EveryOverlayLayerHasAStyle()
	{
		var styles = Overlays.All.Select(Overlays.Style).ToList();
		Assert.Equal(Overlays.All.Length, styles.Count);
		Assert.True(Overlays.Style(Overlays.Layer.Markers).Hidden);
		Assert.False(Overlays.Style(Overlays.Layer.Wards).Hidden);
		// Visible, the zone borders faintest.
		Assert.All(styles, s => Assert.InRange(s.Color.W, 0.2f, 1f));
	}

	[Fact]
	public void LocationMarkersStandOnTheGroundInsideTheArea()
	{
		var s = OverlayTests.Flat();
		var world = new WorldSave { Directory = "none", SaveNumber = 1 };
		world.Locations.Add((new Vector3(10, 35, 10), 1));
		world.Locations.Add((new Vector3(5000, 35, 10), 2));
		s.World = world;
		var o = Overlays.Build(s, null, _ => null);
		Assert.Equal(1, o.Locations);
		var m = o.Lines[Overlays.Layer.Markers];
		Assert.NotEmpty(m);
		// Posts from the ground up 14 m.
		Assert.Equal(35 + 14, m.Where((_, i) => i % 3 == 1).Max(), 1);
	}
}
