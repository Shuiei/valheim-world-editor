using TerrainEditor.Save;
using TerrainEditor.Terrain;

namespace TerrainEditor.Desktop;

// Floors, walls and roofs over a rectangle, laid out piece by piece from the game's pieces (their
// bounds and snap points, PieceCatalog) for Claude's build_floor, build_walls and build_roof. Pure: the
// pieces to put down, in world coordinates (x east, z north, y up), on the 2 m grid of wood pieces.
public static class ClaudeBuilder
{
	public sealed record Door(string Side, float At);

	private static PieceCatalog.Info Info(string prefab) =>
		PieceCatalog.Get(StableHash.Of(prefab)) ?? throw new ArgumentException($"{prefab} is not a piece the editor knows.");

	// Pieces of 2 m across a span: their middles, centred on the span (a span not a multiple of 2 m
	// overhangs a little on both ends).
	private static List<float> Across(float from, float to)
	{
		float len = MathF.Abs(to - from), mid = (from + to) / 2;
		int n = Math.Max(1, (int)MathF.Ceiling(len / 2 - 0.01f));
		return Enumerable.Range(0, n).Select(i => mid - n + 1 + 2 * i).ToList();
	}

	private static ClaudeTools.PieceSpec P(string prefab, float x, float y, float z, float yaw) =>
		new() { Prefab = prefab, X = x, Y = y, Z = z, Yaw = yaw, Snap = false };

	// Floor tiles over the rectangle, their tops at top.
	public static List<ClaudeTools.PieceSpec> Floor(float x0, float z0, float x1, float z1, float top, string material)
	{
		string prefab = material.ToLowerInvariant() switch
		{
			"stone" => "stone_floor_2x2",
			"wood" => "wood_floor",
			_ => throw new ArgumentException("A floor is wood or stone."),
		};
		float y = top - Info(prefab).MaxY;
		return (from x in Across(x0, x1) from z in Across(z0, z1) select P(prefab, x, y, z, 0)).ToList();
	}

	// Walls on the rectangle's edges from bottom up to height (wood: 2 m rows; stone: 1 m rows), with
	// doors (a 2 m wood door in place of the bottom of the 2 m of wall at that point of that side).
	public static List<ClaudeTools.PieceSpec> Walls(float x0, float z0, float x1, float z1, float bottom, float height, string material, IReadOnlyList<Door> doors)
	{
		(float lx, float hx, float lz, float hz) = (MathF.Min(x0, x1), MathF.Max(x0, x1), MathF.Min(z0, z1), MathF.Max(z0, z1));
		(string prefab, float row) = material.ToLowerInvariant() switch
		{
			"stone" => ("stone_wall_2x1", 1f),
			"wood" => ("woodwall", 2f),
			_ => throw new ArgumentException("Walls are wood or stone."),
		};
		int rows = Math.Max(1, (int)MathF.Round(height / row));
		var info = Info(prefab);
		var door = Info("wood_door");
		var list = new List<ClaudeTools.PieceSpec>();
		var sides = new (string Name, bool AlongX, float Fixed, List<float> Spots)[]
		{
			("south", true, lz, Across(lx, hx)), ("north", true, hz, Across(lx, hx)),
			("west", false, lx, Across(lz, hz)), ("east", false, hx, Across(lz, hz)),
		};
		foreach (var d in doors)
		{
			if (!sides.Any(s => s.Name.Equals(d.Side, StringComparison.OrdinalIgnoreCase)))
			{
				throw new ArgumentException($"A door's side is north, south, east or west, not \"{d.Side}\".");
			}
		}
		// Each door in the one 2 m of wall nearest its point.
		var doorAt = new HashSet<(string, float)>();
		foreach (var d in doors)
		{
			var side = sides.First(s => s.Name.Equals(d.Side, StringComparison.OrdinalIgnoreCase));
			doorAt.Add((side.Name, side.Spots.OrderBy(sp => MathF.Abs(sp - d.At)).First()));
		}
		foreach (var (name, alongX, fixedAt, spots) in sides)
		{
			foreach (float spot in spots)
			{
				float x = alongX ? spot : fixedAt, z = alongX ? fixedAt : spot, yaw = alongX ? 0 : 90;
				bool hasDoor = doorAt.Contains((name, spot));
				int skip = 0;
				if (hasDoor)
				{
					list.Add(P("wood_door", x, bottom - door.MinY, z, yaw));
					skip = (int)MathF.Round(2 / row);
				}
				for (int r = skip; r < rows; r++)
				{
					list.Add(P(prefab, x, bottom + r * row - info.MinY, z, yaw));
				}
			}
		}
		return list;
	}

	// A gable roof over the rectangle (its wall lines), the ridge along the longer side: slopes of
	// 26 or 45 degrees whose underside meets the wall lines at eave, a ridge, and (gables) the two ends
	// closed with sloped walls over full and half walls. material: thatch or shingle.
	public static List<ClaudeTools.PieceSpec> Roof(float x0, float z0, float x1, float z1, float eave, int angle, string material, bool gables)
	{
		(float lx, float hx, float lz, float hz) = (MathF.Min(x0, x1), MathF.Max(x0, x1), MathF.Min(z0, z1), MathF.Max(z0, z1));
		string kind = material.ToLowerInvariant() switch
		{
			"thatch" => "wood_roof",
			"shingle" => "darkwood_roof",
			_ => throw new ArgumentException("A roof is thatch or shingle."),
		};
		string suffix = angle switch { 26 => "", 45 => "_45", _ => throw new ArgumentException("A roof is 26 or 45 degrees.") };
		string slopeName = kind + suffix, ridgeName = kind + "_top" + suffix, gableName = "wood_wall_roof" + suffix;
		var slope = Info(slopeName);
		// The slope's edges: low toward its +z, high toward its -z (snap points).
		var low = slope.Snaps.OrderByDescending(s => s[2]).First();
		var high = slope.Snaps.OrderBy(s => s[2]).First();
		float rise = high[1] - low[1], run = low[2] - high[2], pitch = rise / run;
		bool alongX = hx - lx >= hz - lz;
		float cx = (lx + hx) / 2, cz = (lz + hz) / 2;
		float depth = (alongX ? hz - lz : hx - lx) / 2, length = alongX ? hx - lx : hz - lz;
		// Rows each side: from the ridge's edge (1 m out) to at or past the wall line.
		int n = Math.Max(1, (int)MathF.Ceiling((depth - 1) / run - 0.01f));
		float overhang = 1 + n * run - depth;
		float eaveEdge = eave - pitch * overhang, top = eaveEdge + n * rise;
		// World point from (along the ridge, across it).
		(float X, float Z) At(float u, float v) => alongX ? (cx + u, cz + v) : (cx + v, cz + u);
		float plusSide = alongX ? 0 : 90, minusSide = alongX ? 180 : 270, ridgeYaw = alongX ? 0 : 90;
		var list = new List<ClaudeTools.PieceSpec>();
		var spots = alongX ? Across(lx, hx) : Across(lz, hz);
		foreach (float a in spots)
		{
			float u = a - (alongX ? cx : cz);
			for (int k = 1; k <= n; k++)
			{
				float y = top - (k - 1) * rise - high[1];
				var (px, pz) = At(u, 2 * k);
				list.Add(P(slopeName, px, y, pz, plusSide));
				var (mx, mz) = At(u, -2 * k);
				list.Add(P(slopeName, mx, y, mz, minusSide));
			}
			var (rx, rz) = At(u, 0);
			list.Add(P(ridgeName, rx, top, rz, ridgeYaw));
		}
		if (gables)
		{
			// Columns of 2 m from each wall line toward the ridge: walls up to the roof's underside at the
			// column's outer edge, then a sloped wall rising with the roof over the column.
			var gable = Info(gableName);
			var full = Info("woodwall");
			var half = Info("wood_wall_half");
			float wallYaw = alongX ? 90 : 0;
			// The sloped wall rises toward its local +x: toward the ridge from either side.
			float plusGable = alongX ? 90 : 180, minusGable = alongX ? 270 : 0;
			// Walls from y0 up to y1 at a point (full walls, then a half wall when needed).
			void Fill(float x, float z, float y0, float y1)
			{
				while (y1 - y0 >= 2 - 0.01f)
				{
					list.Add(P("woodwall", x, y0 - full.MinY, z, wallYaw));
					y0 += 2;
				}
				if (y1 - y0 >= 1 - 0.01f)
				{
					list.Add(P("wood_wall_half", x, y0 - half.MinY, z, wallYaw));
				}
			}
			foreach (float end in new[] { -length / 2, length / 2 })
			{
				// An odd number of metres from the wall line to the ridge line: the 2 m under the ridge is
				// wall up to the ridge's foot.
				int columns = (int)MathF.Floor(depth / 2 + 0.005f);
				if (MathF.Abs(depth - 2 * columns - 1) < 0.05f)
				{
					var (sx, sz) = At(end, 0);
					Fill(sx, sz, eave, eave + pitch * (depth - 1));
				}
				for (int j = 0; depth - 2 * j - 2 >= -0.01f; j++)
				{
					float baseY = eave + pitch * 2 * j;
					foreach (int sign in new[] { 1, -1 })
					{
						float v = sign * (depth - 2 * j - 1);
						var (gx, gz) = At(end, v);
						Fill(gx, gz, eave, baseY);
						list.Add(P(gableName, gx, baseY, gz, sign > 0 ? plusGable : minusGable));
					}
				}
			}
		}
		return list;
	}
}
