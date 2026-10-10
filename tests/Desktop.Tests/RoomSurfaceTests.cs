using System.Numerics;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

// The cursor on a dungeon's rooms: a ray meets a room's model where its triangles are, with the room
// turned and moved, the model files' mirrored z taken back off, and nothing above the cut.
public sealed class RoomSurfaceTests : IDisposable
{
	private readonly string _dir = Path.Combine(Path.GetTempPath(), "vwe-rooms-" + Guid.NewGuid().ToString("N")[..8]);

	public RoomSurfaceTests()
	{
		foreach (string d in new[] { "pieces", "meshes", "tex" })
		{
			Directory.CreateDirectory(Path.Combine(_dir, d));
		}
		File.WriteAllText(Path.Combine(_dir, "objects.json"), "{}");
		File.WriteAllText(Path.Combine(_dir, "materials.json"), "{}");
		File.WriteAllText(Path.Combine(_dir, "meshinfo.json"), "{\"floor\": {\"v\": 4, \"sub\": [6]}}");
		// A model under a real room's name: a floor 2 m below the room's origin, from x 2 to 6 and z -1
		// to 1 of its own frame (z stored mirrored, as the exporter writes it).
		File.WriteAllText(Path.Combine(_dir, "pieces", "cave_new_endcap02.json"), "{\"parts\": [{\"m\": [1,0,0,0, 0,1,0,0, 0,0,1,0, 0,0,0,1], \"mesh\": \"floor\", \"sub\": 0, \"mat\": \"x\"}]}");
		float[] Vertex(float x, float z) => new[] { x, -2f, -z, 0, 1, 0, 0, 0 };
		var v = Vertex(2, -1).Concat(Vertex(6, -1)).Concat(Vertex(6, 1)).Concat(Vertex(2, 1)).ToArray();
		var bytes = new byte[v.Length * 4 + 24];
		Buffer.BlockCopy(v, 0, bytes, 0, v.Length * 4);
		Buffer.BlockCopy(new uint[] { 0, 1, 2, 0, 2, 3 }, 0, bytes, v.Length * 4, 24);
		File.WriteAllBytes(Path.Combine(_dir, "meshes", "floor.bin"), bytes);
	}

	public void Dispose() => Directory.Delete(_dir, true);

	[Fact]
	public void ARayMeetsATurnedRoomsFloorWhereItIs()
	{
		var models = new ModelStore(_dir);
		var at = new Vector3(100, 5000, 50);
		// Turned 90° about y: the room's +x points to the world's -z (Unity's turn).
		var room = new Dungeons.Placed(StableHash.Of("cave_new_endcap02"), at, Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2));
		var kind = Dungeons.KindOf(StableHash.Of("DG_Cave"))!;
		var dungeons = new[] { new DungeonRooms.Dungeon(0, default, kind, new List<Dungeons.Placed> { room }, null) };
		var down = -Vector3.UnitY;
		// Over the floor: 4 m along the room's x, so 4 m south.
		Assert.Equal(42f, RoomSurfaces.Hit(dungeons, models, at + new Vector3(0, 40, -4), down)!.Value, 3);
		// Where the floor would be unturned, or mirrored: nothing.
		Assert.Null(RoomSurfaces.Hit(dungeons, models, at + new Vector3(4, 40, 0), down));
		Assert.Null(RoomSurfaces.Hit(dungeons, models, at + new Vector3(0, 40, 4), down));
		// Below the cut only.
		Assert.Null(RoomSurfaces.Hit(dungeons, models, at + new Vector3(0, 40, -4), down, maxY: at.Y - 3));
		Assert.NotNull(RoomSurfaces.Hit(dungeons, models, at + new Vector3(0, 40, -4), down, maxY: at.Y));
		// From below too (rooms are seen from both sides), and no store: nothing.
		Assert.Equal(8f, RoomSurfaces.Hit(dungeons, models, at + new Vector3(0, -10, -4), Vector3.UnitY)!.Value, 3);
		Assert.Null(RoomSurfaces.Hit(dungeons, null, at + new Vector3(0, 40, -4), down));
	}
}
