using System.Globalization;

namespace TerrainEditor.Desktop;

// The status bar's cursor readout (the web editor's): where the pointer is on the ground in world
// metres, the ground's height there against the original (and whether it is at the game's ±8 m
// limit), and the zone. Grid coordinates as the 3D view gives them (one point per metre from the
// area's south-west corner).
public static class CursorReadout
{
	public static string Text(WorldScene s, float gx, float gz)
	{
		var c = CultureInfo.InvariantCulture;
		int x = Math.Clamp((int)MathF.Round(gx), 0, s.W - 1), z = Math.Clamp((int)MathF.Round(gz), 0, s.H - 1);
		int g = z * s.W + x;
		int wx = s.X0 * 64 - 32 + x, wz = s.Z0 * 64 - 32 + z;
		string height;
		if (s.Session?.Ground is { } ground)
		{
			float h = ground.HeightOf(g), b = ground.Base[g], d = h - b;
			height = string.Format(c, "ground {0:0.00} m (original {1:0.00}, {2}{3:0.00}{4})", h, b, d >= 0 ? "+" : "", d, ground.AtLimit(g) ? ", at the ±8 m limit" : "");
		}
		else
		{
			height = string.Format(c, "ground {0:0.00} m", s.Heights[g]);
		}
		return string.Format(c, "x {0}, z {1}   {2}   zone {3}, {4}", wx, wz, height, (int)MathF.Floor((wx + 32) / 64f), (int)MathF.Floor((wz + 32) / 64f));
	}
}
