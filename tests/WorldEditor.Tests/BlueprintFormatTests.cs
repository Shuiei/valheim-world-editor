using System.Numerics;
using System.Text.Json.Nodes;
using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

// PlanBuild .blueprint and .vbuild files, read into the clipboard format and written back.
public class BlueprintFormatTests
{
	private const string PlanBuild = """
		#Name:Small hut
		#Creator:Someone
		#Description:"A hut, with a door"
		#Category:Misc
		#SnapPoints
		0;0;0
		#Terrain
		square;2;-0.5;2;3;0;0.5;paved
		#Pieces
		wood_floor;BuildingWorkbench;0;0;0;0;0;0;1;"";1;1;1
		wood_floor;BuildingWorkbench;2;0;0;0;0;0;1;"";1;1;1
		woodwall;BuildingWorkbench;1;0.5;4;0;0.7071068;0;0.7071068;"";1;1;1
		piece_from_a_mod;Misc;0;0;0;0;0;0;1;"";1;1;1
		""";

	[Fact]
	public void ReadsAPlanBuildBlueprint()
	{
		var bp = BlueprintFormats.Parse("hut.blueprint", PlanBuild);
		Assert.Equal("Small hut", bp.Name);
		Assert.Equal("Someone", bp.Creator);
		Assert.Equal("A hut, with a door", bp.Description);
		Assert.Equal(4, bp.Pieces.Count);
		Assert.Single(bp.Terrain);
		Assert.Equal(0, bp.SkippedLines);
		var wall = bp.Pieces[2];
		Assert.Equal("woodwall", wall.Name);
		Assert.Equal(90f, wall.Euler.Y, 2);
		Assert.Equal(new Vector3(1, 0.5f, 4), wall.Position);
	}

	[Fact]
	public void ReadsAVBuildFile()
	{
		var bp = BlueprintFormats.Parse("tower.vbuild", "woodwall 0 0.7071068 0 0.7071068 1.5 0 -2\nwood_floor 0 0 0 1 0 0 0\nbroken line\n");
		Assert.Equal("tower", bp.Name);
		Assert.Equal(2, bp.Pieces.Count);
		Assert.Equal(1, bp.SkippedLines);
		Assert.Equal(new Vector3(1.5f, 0, -2), bp.Pieces[0].Position);
		Assert.Equal(90f, bp.Pieces[0].Euler.Y, 2);
	}

	[Theory]
	[InlineData(0, 90, 0)]
	[InlineData(30, 45, 10)]
	[InlineData(350, 200, 5)]
	[InlineData(0, 0, 270)]
	public void EulerAnglesRoundTripLikeUnity(float x, float y, float z)
	{
		Vector3 back = BlueprintFormats.ToEuler(BlueprintFormats.FromEuler(new Vector3(x, y, z)));
		// The same rotation, whatever the angles chosen to describe it.
		Quaternion a = BlueprintFormats.FromEuler(new Vector3(x, y, z)), b = BlueprintFormats.FromEuler(back);
		Assert.True(MathF.Abs(Quaternion.Dot(a, b)) > 0.9999f, $"({x},{y},{z}) came back as {back}");
	}

	[Fact]
	public void UnityEulerIsYawThenPitchThenRoll()
	{
		// Quaternion.Euler(0, 90, 0) in Unity is (0, 0.7071, 0, 0.7071).
		Quaternion q = BlueprintFormats.FromEuler(new Vector3(0, 90, 0));
		Assert.Equal(0.7071f, q.Y, 3);
		Assert.Equal(0.7071f, q.W, 3);
		// Quaternion.Euler(90, 90, 0) = (0.5, 0.5, -0.5, 0.5).
		Quaternion r = BlueprintFormats.FromEuler(new Vector3(90, 90, 0));
		Assert.Equal(new Vector4(0.5f, 0.5f, -0.5f, 0.5f), new Vector4(MathF.Round(r.X, 3), MathF.Round(r.Y, 3), MathF.Round(r.Z, 3), MathF.Round(r.W, 3)));
	}

	[Fact]
	public void TheClipboardHasTheKnownPiecesAndTheTerrainAsGround()
	{
		var bp = BlueprintFormats.Parse("hut.blueprint", PlanBuild);
		JsonObject clip = BlueprintFormats.ToClip(bp, n => n != "piece_from_a_mod", out var unknown);
		Assert.Equal(new[] { "piece_from_a_mod" }, unknown);
		JsonArray objects = clip["objects"]!.AsArray();
		Assert.Equal(3, objects.Count);
		// Centred on the footprint (pieces and terrain): x from -1 to 5, z from -1 to 5.
		Assert.Equal(-2f, (float)objects[0]!["dx"]!, 3);
		Assert.Equal(0f, (float)objects[0]!["dy"]!, 3);
		int w = (int)clip["w"]!, h = (int)clip["h"]!;
		Assert.Equal((7, 7), (w, h));
		// The middle of the paved square is levelled to its height (-0.5 m below the lowest piece) and paved.
		int i = 3 * w + 3;
		Assert.Equal(-50, (int)clip["rel"]![i]!);
		Assert.Equal(255, (int)clip["pnt"]![i * 4 + 2]!);
		Assert.Equal("Small hut", (string?)clip["name"]);
	}

	[Fact]
	public void WritesPlanBuildAndVBuildThatReadBack()
	{
		var parsed = BlueprintFormats.Parse("hut.blueprint", PlanBuild);
		JsonObject clip = BlueprintFormats.ToClip(parsed, n => n != "piece_from_a_mod", out _);
		string planbuild = BlueprintFormats.Write(clip, "blueprint", "Hut copy", _ => 2);
		Assert.Contains("#Name:Hut copy", planbuild);
		Assert.Contains(";BuildingWorkbench;", planbuild);
		var again = BlueprintFormats.Parse("x.blueprint", planbuild);
		Assert.Equal(3, again.Pieces.Count);
		Assert.Equal(90f, again.Pieces.Single(p => p.Name == "woodwall").Euler.Y, 1);
		var floors = again.Pieces.Where(p => p.Name == "wood_floor").Select(p => p.Position.X).OrderBy(x => x).ToList();
		Assert.Equal(2f, floors[1] - floors[0], 3);
		string vbuild = BlueprintFormats.Write(clip, "vbuild", "Hut copy", _ => 2);
		var v = BlueprintFormats.Parse("x.vbuild", vbuild);
		Assert.Equal(3, v.Pieces.Count);
		Assert.Equal(0, v.SkippedLines);
	}
}
