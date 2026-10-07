namespace TerrainEditor.Desktop;

// Natural-looking detail: seeded gradient noise layered at several scales (fractal noise), the same
// family of noise Valheim's own terrain is built from. The web editor's noise2 and fbm (editor.html),
// with the same seeding, so a seed gives the same pattern in both.
public sealed class Noise
{
	private readonly byte[] _perm = new byte[512];
	public int Seed { get; }

	public Noise(int seed)
	{
		Seed = seed;
		uint a = (uint)seed;
		double Rnd()
		{
			a += 0x6D2B79F5;
			uint t = a;
			t = (t ^ (t >> 15)) * (t | 1);
			t ^= t + (t ^ (t >> 7)) * (t | 61);
			return (t ^ (t >> 14)) / 4294967296.0;
		}
		int[] p = Enumerable.Range(0, 256).ToArray();
		for (int i = 255; i > 0; i--)
		{
			int j = (int)Math.Floor(Rnd() * (i + 1));
			(p[i], p[j]) = (p[j], p[i]);
		}
		for (int i = 0; i < 512; i++)
		{
			_perm[i] = (byte)p[i & 255];
		}
	}

	public float Noise2(float x, float y)
	{
		static float Fade(float t) => t * t * t * (t * (t * 6 - 15) + 10);
		static float Lerp(float a, float b, float t) => a + t * (b - a);
		static float Grad(int h, float x, float y)
		{
			h &= 7;
			float u = h < 4 ? x : y, v = h < 4 ? y : x;
			return ((h & 1) != 0 ? -u : u) + ((h & 2) != 0 ? -v : v);
		}
		int X = (int)MathF.Floor(x), Y = (int)MathF.Floor(y), xi = X & 255, yi = Y & 255;
		x -= X;
		y -= Y;
		float u = Fade(x), v = Fade(y);
		int aa = _perm[_perm[xi] + yi], ab = _perm[_perm[xi] + yi + 1], ba = _perm[_perm[xi + 1] + yi], bb = _perm[_perm[xi + 1] + yi + 1];
		return Lerp(Lerp(Grad(aa, x, y), Grad(ba, x - 1, y), u), Lerp(Grad(ab, x, y - 1), Grad(bb, x - 1, y - 1), u), v) * 0.7f;
	}

	// About -1..1, four octaves.
	public float Fbm(float x, float y)
	{
		float sum = 0, amp = 1, norm = 0;
		for (int o = 0; o < 4; o++)
		{
			sum += Noise2(x, y) * amp;
			norm += amp;
			amp *= 0.5f;
			x = x * 2 + 17.3f;
			y = y * 2 - 9.1f;
		}
		return sum / norm * 1.6f;
	}
}
