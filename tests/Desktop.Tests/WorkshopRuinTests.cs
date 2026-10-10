using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

// A ruin blueprint (a generated dungeon) added onto a plot opened from a building players built: its
// pieces stay a ruin's (no builder) in the blueprint saved, the others keep being the builder's.
// Sets the builder for new pieces (WorldSave.Builder): one world at a time.
[Collection("World files")]
public class WorkshopRuinTests
{
	[Fact]
	public void ARuinAddedOntoABuildingStaysARuin()
	{
		string dir = Path.Combine(Path.GetTempPath(), "vwe-ws-" + Guid.NewGuid().ToString("N")[..8]);
		long saved = WorldSave.Builder;
		try
		{
			WorldSave.Builder = 4242;
			Directory.CreateDirectory(dir);
			string hut = Path.Combine(dir, "hut.blueprint");
			File.WriteAllText(hut, "#Name:Hut\n#Creator:test\n#HomesteadVersion:1\n#Pieces\nwood_floor;Building;0;0;0;0;0;0;1;\"\";1;1;1\n");
			System.Text.Json.Nodes.JsonObject Obj(string name) => new()
			{
				["name"] = name, ["dx"] = 0, ["dy"] = 0, ["dz"] = 0, ["rx"] = 0, ["ry"] = 0, ["rz"] = 0, ["scale"] = 0,
				["data"] = BlueprintFormats.DataJson(new[] { ObjectField.NoBuilder }),
			};
			string vault = Path.Combine(dir, "vault.blueprint");
			File.WriteAllText(vault, Homestead.Write(new System.Text.Json.Nodes.JsonObject { ["objects"] = new System.Text.Json.Nodes.JsonArray(Obj("stone_wall_4x2")) },
				"Vault", "test", null, DateTime.Now));

			var scene = Workshop.Create("Workshop");
			var (_, _, lift, _) = Workshop.Open(scene.Session!, hut);
			Workshop.Add(scene.Session!, vault, null, new System.Numerics.Vector2(10, 0));
			Assert.False(scene.Ruin);

			var again = BlueprintFormats.Parse("hut.blueprint", Homestead.Write(Workshop.Building(scene, "Hut", lift), "Hut", "test", null, DateTime.Now));
			Assert.False(again.Ruin);
			Assert.Contains(ObjectField.NoBuilder, again.Pieces.Single(p => p.Name == "stone_wall_4x2").Data!);
			Assert.DoesNotContain(ObjectField.NoBuilder, again.Pieces.Single(p => p.Name == "wood_floor").Data ?? new List<ObjectField>());
		}
		finally
		{
			WorldSave.Builder = saved;
			if (Directory.Exists(dir))
			{
				Directory.Delete(dir, recursive: true);
			}
		}
	}
}
