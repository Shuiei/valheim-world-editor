using System.Collections.Concurrent;
using System.Numerics;
using TerrainEditor.Editing;

namespace TerrainEditor.Desktop;

// Where a ray meets the rooms of the area's dungeons: their models' triangles (the game's look), so
// the cursor can point at a room's floor or wall, as at the ground outside (building in a dungeon,
// measuring there). Pieces resting on a room are held by it in game: its walls and floors are not
// pieces, so they count as ground for the support check (WearNTear.UpdateSupport).
public static class RoomSurfaces
{
	// A room model's triangles in the room's own frame (Unity's axes), with their box.
	private sealed record Mesh(Vector3[] Corners, Vector3 Min, Vector3 Max);

	private static readonly ConcurrentDictionary<(ModelStore, int), Mesh?> Meshes = new();

	private static Mesh? MeshOf(ModelStore models, int room) => Meshes.GetOrAdd((models, room), key =>
	{
		var (m, hash) = key;
		if (Dungeons.RoomOf(hash) is not { } r || m.LoadModel(r.Name) is not { } model)
		{
			return null;
		}
		var corners = new List<Vector3>();
		foreach (var part in model.Parts)
		{
			if (m.LoadMesh(part.Mesh) is not { } md || part.Sub >= md.Submeshes.Length)
			{
				continue;
			}
			var v = md.Vertices;
			// Vertices are 8 floats (position, normal, uv), mirrored in z like the part's matrix; the
			// mirror is taken back off for the room's own (Unity) frame.
			Vector3 At(uint i)
			{
				var p = Vector3.Transform(new Vector3(v[i * 8], v[i * 8 + 1], v[i * 8 + 2]), part.Matrix);
				return new Vector3(p.X, p.Y, -p.Z);
			}
			foreach (uint i in md.Submeshes[part.Sub])
			{
				corners.Add(At(i));
			}
		}
		if (corners.Count < 3)
		{
			return null;
		}
		var min = corners.Aggregate(Vector3.Min);
		var max = corners.Aggregate(Vector3.Max);
		return new Mesh(corners.ToArray(), min, max);
	});

	// The distance along the ray (world space, d normalised) to the first room surface it meets below
	// maxY (the cut), or null.
	public static float? Hit(IReadOnlyList<DungeonRooms.Dungeon> dungeons, ModelStore? models, Vector3 o, Vector3 d, float maxY = float.MaxValue)
	{
		if (models == null)
		{
			return null;
		}
		float best = float.MaxValue;
		foreach (var dg in dungeons)
		{
			foreach (var room in dg.Rooms)
			{
				if (MeshOf(models, room.Hash) is not { } mesh)
				{
					continue;
				}
				var inv = Quaternion.Inverse(room.Rotation);
				var lo = Vector3.Transform(o - room.Position, inv);
				var ld = Vector3.Transform(d, inv);
				if (Picking.HitBox(lo, ld, mesh.Min - new Vector3(0.01f), mesh.Max + new Vector3(0.01f)) is not float enter || enter > best)
				{
					continue;
				}
				var c = mesh.Corners;
				for (int i = 0; i + 2 < c.Length; i += 3)
				{
					if (Triangle(lo, ld, c[i], c[i + 1], c[i + 2]) is float t && t < best && o.Y + d.Y * t <= maxY)
					{
						best = t;
					}
				}
			}
		}
		return best < float.MaxValue ? best : null;
	}

	// Möller–Trumbore, both sides (the game's rooms are seen from inside and out).
	private static float? Triangle(Vector3 o, Vector3 d, Vector3 a, Vector3 b, Vector3 c)
	{
		Vector3 e1 = b - a, e2 = c - a, p = Vector3.Cross(d, e2);
		float det = Vector3.Dot(e1, p);
		if (MathF.Abs(det) < 1e-9f)
		{
			return null;
		}
		float inv = 1f / det;
		Vector3 s = o - a;
		float u = Vector3.Dot(s, p) * inv;
		if (u < 0 || u > 1)
		{
			return null;
		}
		Vector3 q = Vector3.Cross(s, e1);
		float v = Vector3.Dot(d, q) * inv;
		if (v < 0 || u + v > 1)
		{
			return null;
		}
		float t = Vector3.Dot(e2, q) * inv;
		return t > 1e-4f ? t : null;
	}
}
