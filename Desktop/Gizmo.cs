using System.Numerics;

namespace TerrainEditor.Desktop;

// The Select tool's handles, like the web editor's (editor/transform.js, after Blender's): a red X
// (east), green Y (up) and blue Z (north) arrow at the middle of the selection, and an orange ring
// flat around it to turn it. Sizes are for a gizmo of scale 1 (the view scales it with the distance,
// so it keeps its size on screen). View space: x east, y up, z south.
public static class Gizmo
{
	public enum Handle { X, Y, Z, Ring }

	public static Vector3 Axis(Handle h) => h switch
	{
		Handle.X => Vector3.UnitX,
		Handle.Y => Vector3.UnitY,
		_ => -Vector3.UnitZ,
	};

	public static Vector4 Color(Handle h, bool hot) => hot ? new Vector4(1, 0.88f, 0.29f, 1) : h switch
	{
		Handle.X => new Vector4(1, 0.3f, 0.37f, 1),
		Handle.Y => new Vector4(0.43f, 0.88f, 0.35f, 1),
		Handle.Z => new Vector4(0.3f, 0.55f, 1, 1),
		_ => new Vector4(1, 0.65f, 0.3f, 0.85f),
	};

	// The arrows start a bit out from the middle, so the object itself can still be grabbed.
	private const float ShaftFrom = 0.3f, ShaftTo = 1.0f, ShaftRadius = 0.025f, HeadTo = 1.22f, HeadRadius = 0.08f;
	public const float RingRadius = 1.45f;

	// Triangles (x, y, z per corner) of a handle at scale 1 around 0.
	public static float[] Mesh(Handle h)
	{
		var v = new List<float>();
		void Tri(Vector3 a, Vector3 b, Vector3 c)
		{
			foreach (var p in new[] { a, b, c })
			{
				v.Add(p.X);
				v.Add(p.Y);
				v.Add(p.Z);
			}
		}
		const int n = 16;
		if (h == Handle.Ring)
		{
			const float r = 0.022f;
			const int m = 96;
			Vector3 P(int i, int j)
			{
				float a = i * MathF.Tau / m, b = j * MathF.Tau / 6;
				float rr = RingRadius + r * MathF.Cos(b);
				return new Vector3(rr * MathF.Cos(a), r * MathF.Sin(b), rr * MathF.Sin(a));
			}
			for (int i = 0; i < m; i++)
			{
				for (int j = 0; j < 6; j++)
				{
					Tri(P(i, j), P(i + 1, j), P(i + 1, j + 1));
					Tri(P(i, j), P(i + 1, j + 1), P(i, j + 1));
				}
			}
			return v.ToArray();
		}
		// Along +y, then turned onto the axis.
		var turn = h switch
		{
			Handle.X => Matrix4x4.CreateRotationZ(-MathF.PI / 2),
			Handle.Z => Matrix4x4.CreateRotationX(-MathF.PI / 2),
			_ => Matrix4x4.Identity,
		};
		Vector3 T(Vector3 p) => Vector3.Transform(p, turn);
		Vector3 Round(float r, float y, int i) => T(new Vector3(r * MathF.Cos(i * MathF.Tau / n), y, r * MathF.Sin(i * MathF.Tau / n)));
		for (int i = 0; i < n; i++)
		{
			Tri(Round(ShaftRadius, ShaftFrom, i), Round(ShaftRadius, ShaftTo, i), Round(ShaftRadius, ShaftTo, i + 1));
			Tri(Round(ShaftRadius, ShaftFrom, i), Round(ShaftRadius, ShaftTo, i + 1), Round(ShaftRadius, ShaftFrom, i + 1));
			Tri(Round(HeadRadius, ShaftTo - 0.02f, i), T(new Vector3(0, HeadTo, 0)), Round(HeadRadius, ShaftTo - 0.02f, i + 1));
			Tri(Round(HeadRadius, ShaftTo - 0.02f, i), Round(HeadRadius, ShaftTo - 0.02f, i + 1), T(new Vector3(0, ShaftTo - 0.02f, 0)));
		}
		return v.ToArray();
	}

	// The handle a ray from the mouse grabs (the arrows a little thicker than drawn), or null.
	public static Handle? Hit(Vector3 o, Vector3 d, Vector3 centre, float scale)
	{
		Handle? best = null;
		float bestT = float.MaxValue;
		foreach (var h in new[] { Handle.X, Handle.Y, Handle.Z })
		{
			var a = Axis(h);
			var (t, s, dist) = Closest(o, d, centre, a);
			if (s >= ShaftFrom * scale && s <= HeadTo * scale && dist <= 0.09f * scale && t > 0 && t < bestT)
			{
				best = h;
				bestT = t;
			}
		}
		if (best != null)
		{
			return best;
		}
		// The ring: where the ray meets the flat plane through the middle, near the ring.
		if (MathF.Abs(d.Y) > 1e-4f)
		{
			float t = (centre.Y - o.Y) / d.Y;
			if (t > 0)
			{
				var p = o + d * t;
				float r = MathF.Sqrt((p.X - centre.X) * (p.X - centre.X) + (p.Z - centre.Z) * (p.Z - centre.Z));
				if (MathF.Abs(r - RingRadius * scale) <= 0.11f * scale)
				{
					return Handle.Ring;
				}
			}
		}
		return null;
	}

	// The closest points of the ray (o + d t) and the axis line (c + a s): t, s and the distance between them.
	public static (float T, float S, float Distance) Closest(Vector3 o, Vector3 d, Vector3 c, Vector3 a)
	{
		var w = o - c;
		float b = Vector3.Dot(a, d), den = 1 - b * b;
		if (den < 1e-6f)
		{
			return (0, Vector3.Dot(w, a), Vector3.Cross(w, a).Length());
		}
		float dw = Vector3.Dot(d, w), aw = Vector3.Dot(a, w);
		float t = (b * aw - dw) / den, s = (aw - b * dw) / den;
		return (t, s, Vector3.Distance(o + d * t, c + a * s));
	}

	// How far along the axis (through c) the mouse ray passes closest, or null looking straight down it.
	public static float? Along(Vector3 o, Vector3 d, Vector3 c, Vector3 a)
	{
		float b = Vector3.Dot(a, d);
		return 1 - b * b < 1e-4f ? null : Closest(o, d, c, a).S;
	}

	// The heading (Unity yaw, degrees: clockwise from north seen from above) from the middle to where
	// the ray meets the flat plane through it, or null.
	public static float? Heading(Vector3 o, Vector3 d, Vector3 centre)
	{
		if (MathF.Abs(d.Y) < 1e-4f)
		{
			return null;
		}
		float t = (centre.Y - o.Y) / d.Y;
		if (t <= 0)
		{
			return null;
		}
		var p = o + d * t;
		return MathF.Atan2(p.X - centre.X, -(p.Z - centre.Z)) * 180 / MathF.PI;
	}
}
