using System.Numerics;

namespace TerrainEditor.Desktop;

// Finding what is under the mouse: a ray from the camera through the pointer, against the objects'
// boxes and the ground. All in view space (see WorldScene).
public static class Picking
{
	// The ray through a point of the view (nx, ny from -1 to 1, y up) for the camera's view × projection.
	public static (Vector3 Origin, Vector3 Dir) Ray(Matrix4x4 viewProj, float nx, float ny)
	{
		Matrix4x4.Invert(viewProj, out var inv);
		Vector3 Unproject(float z)
		{
			var p = Vector4.Transform(new Vector4(nx, ny, z, 1), inv);
			return new Vector3(p.X, p.Y, p.Z) / p.W;
		}
		var near = Unproject(-1);
		return (near, Vector3.Normalize(Unproject(1) - near));
	}

	// Where the ray enters a box (distance along it), or null.
	public static float? HitBox(Vector3 o, Vector3 d, Vector3 min, Vector3 max)
	{
		float t0 = 0, t1 = float.MaxValue;
		for (int a = 0; a < 3; a++)
		{
			float oa = a == 0 ? o.X : a == 1 ? o.Y : o.Z, da = a == 0 ? d.X : a == 1 ? d.Y : d.Z;
			float lo = a == 0 ? min.X : a == 1 ? min.Y : min.Z, hi = a == 0 ? max.X : a == 1 ? max.Y : max.Z;
			if (MathF.Abs(da) < 1e-9f)
			{
				if (oa < lo || oa > hi)
				{
					return null;
				}
				continue;
			}
			float ta = (lo - oa) / da, tb = (hi - oa) / da;
			if (ta > tb)
			{
				(ta, tb) = (tb, ta);
			}
			t0 = MathF.Max(t0, ta);
			t1 = MathF.Min(t1, tb);
			if (t0 > t1)
			{
				return null;
			}
		}
		return t0;
	}

	// The box around a box moved by a matrix (its eight corners).
	public static (Vector3 Min, Vector3 Max) Transform(Vector3 min, Vector3 max, Matrix4x4 m)
	{
		Vector3 lo = new(float.MaxValue), hi = new(float.MinValue);
		for (int i = 0; i < 8; i++)
		{
			var c = Vector3.Transform(new Vector3((i & 1) == 0 ? min.X : max.X, (i & 2) == 0 ? min.Y : max.Y, (i & 4) == 0 ? min.Z : max.Z), m);
			lo = Vector3.Min(lo, c);
			hi = Vector3.Max(hi, c);
		}
		return (lo, hi);
	}

	// Where the ray meets the ground (distance), stepping half a metre then narrowing down, or null.
	public static float? HitGround(WorldScene s, Vector3 o, Vector3 d, float maxDist = 4000)
	{
		float Below(float t)
		{
			var p = o + d * t;
			return p.Y - HeightAt(s, p.X, p.Z);
		}
		float prev = 0;
		float above = Below(0);
		for (float t = 0.5f; t <= maxDist; t += 0.5f)
		{
			float b = Below(t);
			if (b <= 0 && above > 0)
			{
				float lo = prev, hi = t;
				for (int i = 0; i < 20; i++)
				{
					float mid = (lo + hi) / 2;
					if (Below(mid) > 0) lo = mid; else hi = mid;
				}
				return (lo + hi) / 2;
			}
			prev = t;
			above = b;
		}
		return null;
	}

	// The ground's height under a view-space point (between the grid points), or very low outside.
	public static float HeightAt(WorldScene s, float x, float z)
	{
		float gx = x + (s.W - 1) / 2f, gz = -z + (s.H - 1) / 2f;
		if (gx < 0 || gz < 0 || gx > s.W - 1 || gz > s.H - 1)
		{
			return -1000;
		}
		int x0 = Math.Min((int)gx, s.W - 2), z0 = Math.Min((int)gz, s.H - 2);
		float tx = gx - x0, tz = gz - z0;
		float H(int a, int b) => s.Heights[b * s.W + a];
		return (H(x0, z0) * (1 - tx) + H(x0 + 1, z0) * tx) * (1 - tz) + (H(x0, z0 + 1) * (1 - tx) + H(x0 + 1, z0 + 1) * tx) * tz;
	}
}
