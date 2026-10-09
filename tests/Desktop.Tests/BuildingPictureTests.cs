using System.Numerics;
using SkiaSharp;
using TerrainEditor.App;
using TerrainEditor.Terrain;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

// A blueprint's picture (BuildingPicture) without the game's models: its pieces' boxes, filling the
// middle and leaving the corners clear; and what a building costs in game (PieceCost).
public class BuildingPictureTests
{
	private static readonly List<BuildingPicture.Piece> Hut = new()
	{
		new("wood_floor", new Vector3(0, 0, 0), Vector3.Zero, 0),
		new("woodwall", new Vector3(0, 1, -1), Vector3.Zero, 0),
		new("woodwall", new Vector3(1, 1, 0), new Vector3(0, 90, 0), 0),
		new("no_such_piece_xyz", new Vector3(-1, 0, 0), Vector3.Zero, 0),
	};

	[Fact]
	public void TheBuildingFillsThePictureAndItsCornersAreClear()
	{
		byte[] png = BuildingPicture.Draw(Hut, null, 128);
		using var bmp = SKBitmap.Decode(png);
		Assert.Equal((128, 128), (bmp.Width, bmp.Height));
		Assert.Equal(0, bmp.GetPixel(2, 2).Alpha);
		Assert.Equal(0, bmp.GetPixel(125, 125).Alpha);
		int opaque = 0;
		for (int y = 0; y < 128; y++)
		{
			for (int x = 0; x < 128; x++)
			{
				opaque += bmp.GetPixel(x, y).Alpha > 200 ? 1 : 0;
			}
		}
		Assert.InRange(opaque, 128 * 128 / 8, 128 * 128 * 3 / 4);
		// Lit from one side: not all one colour.
		Assert.True(Enumerable.Range(0, 128).Select(x => bmp.GetPixel(x, 64)).Where(c => c.Alpha > 200).Distinct().Count() > 2);
		// Nothing to draw: a clear picture.
		using var none = SKBitmap.Decode(BuildingPicture.Draw(new List<BuildingPicture.Piece>(), null, 32));
		Assert.Equal(0, none.GetPixel(16, 16).Alpha);
	}

	// With the game's models (copied on this computer): written to look at, when asked.
	[Fact]
	public void WithTheGamesModelsWhenThereAreSome()
	{
		string? dir = Environment.GetEnvironmentVariable("VWE_TEST_MODELS"), outFile = Environment.GetEnvironmentVariable("VWE_TEST_PICTURE");
		if (dir == null || outFile == null || !File.Exists(Path.Combine(dir, "meshinfo.json")))
		{
			return;
		}
		var models = new ModelStore(dir);
		var pieces = Hut.ToList();
		if (Environment.GetEnvironmentVariable("VWE_TEST_BLUEPRINT") is string bp)
		{
			var parsed = BlueprintFormats.Parse(bp, File.ReadAllText(bp));
			pieces = parsed.Pieces.Select(p => new BuildingPicture.Piece(p.Name, p.Position, p.Euler, p.Scale)).ToList();
		}
		File.WriteAllBytes(outFile, BuildingPicture.Draw(pieces, models));
		File.WriteAllBytes(Path.ChangeExtension(outFile, ".boxes.png"), BuildingPicture.Draw(pieces, null));
	}

	[Fact]
	public void ABuildingCostsItsPiecesResources()
	{
		var cost = PieceCost.Of(new[] { "woodwall", "woodwall", "wood_floor", "stone_wall_2x1", "no_such_piece_xyz" });
		Assert.Equal(("Wood", "Wood", 6), cost.Materials[0]);
		Assert.Contains(cost.Materials, m => m.Item == "Stone" && m.Amount == 4);
		Assert.Equal(new[] { "Stonecutter", "Workbench" }, cost.Stations);
		Assert.Equal(1, cost.Unknown["no_such_piece_xyz"]);
		Assert.Equal("Wood 6 · Stone 4 (needs Stonecutter, Workbench)", cost.Describe());
		Assert.Equal("Wood Wall", PieceCost.PieceName("woodwall"));
		Assert.Equal("Free", PieceCost.Of(Array.Empty<string>()).Describe());
		Assert.Equal("Cost unknown (pieces from mods?)", PieceCost.Of(new[] { "no_such_piece_xyz" }).Describe());
	}
}
