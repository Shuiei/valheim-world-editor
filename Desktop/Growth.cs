using System.Numerics;
using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;

namespace TerrainEditor.Desktop;

// Growing the game's own vegetation (Regrow: the world generator's rules, on the ground as it is now)
// over a rectangle of the world, for the Area tool's Regrow and the Mountain tool: the spots kept by
// keep, not where something already stands (a tree still there is not doubled) or near buildings.
public static class Growth
{
	// Null when it cannot be done (a message says why); x0..x1, z0..z1 in world metres, over up to
	// maxZones zones (the work grows with them).
	public static async Task<List<Regrow.Spot>?> Spots(EditSession session, float x0, float z0, float x1, float z1, Func<Regrow.Spot, bool> keep, Func<int, string?> nameOf, Action<string> message, int maxZones = 16)
	{
		var s = session.Scene;
		if (s.Terrain is not { } terrain || s.World == null)
		{
			message("Regrow needs the world's generator (not available here).");
			return null;
		}
		static int Zone(float v) => (int)MathF.Floor((v + 32) / 64);
		int zx0 = Zone(x0), zx1 = Zone(x1), zz0 = Zone(z0), zz1 = Zone(z1);
		if ((zx1 - zx0 + 1) * (zz1 - zz0 + 1) > maxZones)
		{
			int side = (int)Math.Sqrt(maxZones);
			message($"Choose a smaller area: regrow works on up to {maxZones} zones ({side * 64} x {side * 64} m) at once.");
			return null;
		}
		message("Working out what the game grows here…");
		var world = s.World;
		var spots = await Task.Run(() => Regrow.Zones(terrain, session.Edits, world.Seed, zx0, zz0, zx1, zz1).Where(p => world.CanCreate(StableHash.Of(p.Name))).ToList());
		List<(float X, float Z, string? Name, ObjectKind Kind)> standing;
		lock (s.Things)
		{
			standing = s.Things.Where(t => !t.Gone).Select(t => (t.Position.X, t.Position.Z, nameOf(t.Prefab), ObjectKinds.Of(nameOf(t.Prefab), t.Piece, t.Tamed))).ToList();
		}
		bool Near(Regrow.Spot o, float d, Func<string?, ObjectKind, bool> test) =>
			standing.Any(t => MathF.Abs(t.X - o.X) < d && MathF.Abs(t.Z - o.Z) < d && MathF.Sqrt((t.X - o.X) * (t.X - o.X) + (t.Z - o.Z) * (t.Z - o.Z)) < d && test(t.Name, t.Kind));
		return spots.Where(o => keep(o) && !Near(o, 1, (_, _) => true) && !Near(o, 3, (n, _) => n == o.Name) && !Near(o, 4, (_, k) => k == ObjectKind.Buildings)).ToList();
	}

	// The spots as new objects, in one undo step.
	public static void Commit(EditSession session, string label, IEnumerable<Regrow.Spot> spots) =>
		session.Commit(label, null, Array.Empty<int>(), spots.Select(o => (new NewObject(0, StableHash.Of(o.Name), new Vector3(o.X, o.Y, o.Z), new Vector3(o.Rx, o.Ry, o.Rz), MathF.Abs(o.Scale - 1) < 1e-4f ? 0 : o.Scale), false)).ToList());
}
