
// Re-implementations of the two Unity engine functions the world generator relies on that are
// native code inside the engine: UnityEngine.Random (Xorshift128) and Mathf.PerlinNoise.
// Both are verified bit-for-bit against values recorded in the real game (TerrainDump plugin).
namespace ValheimGen
{
	public static class UnityPerlin
	{
		private static readonly int[] Perm = BuildPermutation();

		private static int[] BuildPermutation()
		{
			int[] p =
			{
				151, 160, 137, 91, 90, 15, 131, 13, 201, 95, 96, 53, 194, 233, 7, 225, 140, 36, 103, 30, 69, 142, 8, 99, 37, 240, 21, 10, 23,
				190, 6, 148, 247, 120, 234, 75, 0, 26, 197, 62, 94, 252, 219, 203, 117, 35, 11, 32, 57, 177, 33, 88, 237, 149, 56, 87, 174,
				20, 125, 136, 171, 168, 68, 175, 74, 165, 71, 134, 139, 48, 27, 166, 77, 146, 158, 231, 83, 111, 229, 122, 60, 211, 133, 230,
				220, 105, 92, 41, 55, 46, 245, 40, 244, 102, 143, 54, 65, 25, 63, 161, 1, 216, 80, 73, 209, 76, 132, 187, 208, 89, 18, 169,
				200, 196, 135, 130, 116, 188, 159, 86, 164, 100, 109, 198, 173, 186, 3, 64, 52, 217, 226, 250, 124, 123, 5, 202, 38, 147,
				118, 126, 255, 82, 85, 212, 207, 206, 59, 227, 47, 16, 58, 17, 182, 189, 28, 42, 223, 183, 170, 213, 119, 248, 152, 2, 44,
				154, 163, 70, 221, 153, 101, 155, 167, 43, 172, 9, 129, 22, 39, 253, 19, 98, 108, 110, 79, 113, 224, 232, 178, 185, 112,
				104, 218, 246, 97, 228, 251, 34, 242, 193, 238, 210, 144, 12, 191, 179, 162, 241, 81, 51, 145, 235, 249, 14, 239, 107, 49,
				192, 214, 31, 181, 199, 106, 157, 184, 84, 204, 176, 115, 121, 50, 45, 127, 4, 150, 254, 138, 236, 205, 93, 222, 114, 67,
				29, 24, 72, 243, 141, 128, 195, 78, 66, 215, 61, 156, 180
			};
			int[] result = new int[512];
			for (int i = 0; i < 512; i++)
			{
				result[i] = p[i & 255];
			}
			return result;
		}

		// Everything below mirrors the machine code of Mathf.PerlinNoise in UnityPlayer.so
		// (Unity 6000.0): Ken Perlin's improved noise in 3D with z = 0, evaluated on |x| and |y|,
		// in single precision and in the same operation order. Matches 1,207 game samples bit for bit.
		private static float Fade(float t)
		{
			t = Math.Min(1f, t);
			return t * t * t * ((t * 6f + -15f) * t + 10f);
		}

		private static float Lerp(float t, float a, float b) => a + t * (b - a);

		private static float Grad(int hash, float x, float y)
		{
			int h = hash & 15;
			float u = h < 8 ? x : y;
			float v = h < 4 ? y : ((h | 2) == 14 ? x : 0f);
			return ((h & 1) == 0 ? u : -u) + ((h & 2) == 0 ? v : -v);
		}

		public static float Noise(float x, float y)
		{
			x = Math.Abs(x);
			y = Math.Abs(y);
			int xi = (int)x;
			int yi = (int)y;
			x -= xi;
			y -= yi;
			xi &= 255;
			yi &= 255;
			int aa = Perm[Perm[Perm[xi] + yi]];
			int ba = Perm[Perm[Perm[xi + 1] + yi]];
			int ab = Perm[Perm[Perm[xi] + yi + 1]];
			int bb = Perm[Perm[Perm[xi + 1] + yi + 1]];
			float u = Fade(x);
			float v = Fade(y);
			float x1 = x + -1f;
			float y1 = y + -1f;
			float n = Lerp(v, Lerp(u, Grad(aa, x, y), Grad(ba, x1, y)), Lerp(u, Grad(ab, x, y1), Grad(bb, x1, y1)));
			return (n + 0.69f) / 1.483f;
		}
	}

	namespace UnityEngine
	{
		// Unity's UnityEngine.Random: Xorshift128 with Unity's seeding and float conversion.
		public static class Random
		{
			public struct State
			{
				internal uint s0;

				internal uint s1;

				internal uint s2;

				internal uint s3;
			}

			private static uint _s0;

			private static uint _s1;

			private static uint _s2;

			private static uint _s3;

			public static State state
			{
				get => new() { s0 = _s0, s1 = _s1, s2 = _s2, s3 = _s3 };
				set
				{
					_s0 = value.s0;
					_s1 = value.s1;
					_s2 = value.s2;
					_s3 = value.s3;
				}
			}

			public static void InitState(int seed)
			{
				_s0 = (uint)seed;
				_s1 = _s0 * 1812433253u + 1u;
				_s2 = _s1 * 1812433253u + 1u;
				_s3 = _s2 * 1812433253u + 1u;
			}

			public static uint NextUInt()
			{
				uint t = _s0 ^ (_s0 << 11);
				_s0 = _s1;
				_s1 = _s2;
				_s2 = _s3;
				_s3 = _s3 ^ (_s3 >> 19) ^ t ^ (t >> 8);
				return _s3;
			}

			public static float value => (NextUInt() & 0x7FFFFF) * (1f / 8388607f);

			public static float Range(float min, float max)
			{
				// Unity interpolates from max to min, not min to max.
				float t = value;
				return t * min + (1f - t) * max;
			}

			public static int Range(int min, int max)
			{
				if (min < max)
				{
					return (int)((uint)min + NextUInt() % (uint)(max - min));
				}
				if (min > max)
				{
					return (int)((uint)min - NextUInt() % (uint)(min - max));
				}
				return min;
			}

			public static ValheimGen.Vector2 insideUnitCircle
			{
				get
				{
					// Native engine code: single-precision trigonometry.
					float angle = Range(0f, 1f) * (float)(Math.PI * 2.0);
					float radius = MathF.Sqrt(Range(0f, 1f));
					return new ValheimGen.Vector2(radius * MathF.Cos(angle), radius * MathF.Sin(angle));
				}
			}
		}
	}
}
