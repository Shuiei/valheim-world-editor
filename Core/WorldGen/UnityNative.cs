
// Re-implementations of the two Unity engine functions the world generator relies on that are
// native code inside the engine: UnityEngine.Random (Xorshift128) and Mathf.PerlinNoise.
// Both are verified bit-for-bit against values recorded in the real game (TerrainDump plugin).
namespace ValheimGen
{
    /// <summary>
    /// Unity's Mathf.PerlinNoise, bit for bit.
    /// </summary>
    public static class UnityPerlin
    {
        /// <summary>
        /// Ken Perlin's permutation, repeated to 512 entries so lookups need no wrap.
        /// </summary>
        private static readonly int[] Perm = BuildPermutation();

        /// <summary>
        /// Ken Perlin's 256-entry permutation, twice over.
        /// </summary>
        private static int[] BuildPermutation()
        {
            int[] p =
            [
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
            ];
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
        /// <summary>
        /// The fade curve 6t⁵ - 15t⁴ + 10t³, t held to at most 1.
        /// </summary>
        private static float Fade(float t)
        {
            t = Math.Min(1f, t);
            return t * t * t * ((t * 6f + -15f) * t + 10f);
        }

        /// <summary>
        /// a to b by t (not clamped).
        /// </summary>
        private static float Lerp(float t, float a, float b) => a + t * (b - a);

        /// <summary>
        /// The improved noise gradient for a hash, dotted with (x, y, 0).
        /// </summary>
        private static float Grad(int hash, float x, float y)
        {
            int h = hash & 15;
            float u = h < 8 ? x : y;
            float v = h < 4 ? y : ((h | 2) == 14 ? x : 0f);
            return ((h & 1) == 0 ? u : -u) + ((h & 2) == 0 ? v : -v);
        }

        /// <summary>
        /// The table read without bounds checks (every index stays below 512).
        /// </summary>
        private static int P(int i) => System.Runtime.CompilerServices.Unsafe.Add(ref System.Runtime.InteropServices.MemoryMarshal.GetArrayDataReference(Perm), i);

        /// <summary>
        /// Perlin noise at (x, y), about 0..1 (Unity's own scaling). Taken at |x| and |y|, so it is mirrored about
        /// both axes.
        /// </summary>
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
            int aa = P(P(P(xi) + yi));
            int ba = P(P(P(xi + 1) + yi));
            int ab = P(P(P(xi) + yi + 1));
            int bb = P(P(P(xi + 1) + yi + 1));
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
        /// <summary>
        /// Unity's UnityEngine.Random: Xorshift128 with Unity's seeding and float conversion. The game only
        /// draws from it on the main thread; here worlds are made and regrown on several threads at once, so
        /// each thread has its own state (shared, they would mix their draws and make other rivers).
        /// </summary>
        public static class Random
        {
            /// <summary>
            /// The generator's four state words, as Random.state saves and restores them.
            /// </summary>
            public struct State : IEquatable<State>
            {
                /// <summary>
                /// First state word.
                /// </summary>
                internal uint s0;

                /// <summary>
                /// Second state word.
                /// </summary>
                internal uint s1;

                /// <summary>
                /// Third state word.
                /// </summary>
                internal uint s2;

                /// <summary>
                /// Fourth state word.
                /// </summary>
                internal uint s3;

                /// <summary>Equal when the four state words are equal.</summary>
                public readonly bool Equals(State other) => s0 == other.s0 && s1 == other.s1 && s2 == other.s2 && s3 == other.s3;

                /// <summary>Equal to another state with the same four words.</summary>
                public override readonly bool Equals(object? obj) => obj is State v && Equals(v);

                /// <summary>A hash of the four words, equal for equal states.</summary>
                public override readonly int GetHashCode() => HashCode.Combine(s0, s1, s2, s3);
            }

            /// <summary>
            /// This thread's state, first word.
            /// </summary>
            [ThreadStatic]
            private static uint _s0;

            /// <summary>
            /// This thread's state, second word.
            /// </summary>
            [ThreadStatic]
            private static uint _s1;

            /// <summary>
            /// This thread's state, third word.
            /// </summary>
            [ThreadStatic]
            private static uint _s2;

            /// <summary>
            /// This thread's state, fourth word.
            /// </summary>
            [ThreadStatic]
            private static uint _s3;

            /// <summary>
            /// This thread's state, to save and restore around draws (as the game does).
            /// </summary>
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

            /// <summary>
            /// Seeds this thread's state as Unity's Random.InitState does.
            /// </summary>
            public static void InitState(int seed)
            {
                _s0 = (uint)seed;
                _s1 = _s0 * 1812433253u + 1u;
                _s2 = _s1 * 1812433253u + 1u;
                _s3 = _s2 * 1812433253u + 1u;
            }

            /// <summary>
            /// The next raw 32-bit draw (one Xorshift128 step).
            /// </summary>
            public static uint NextUInt()
            {
                uint t = _s0 ^ (_s0 << 11);
                _s0 = _s1;
                _s1 = _s2;
                _s2 = _s3;
                _s3 = _s3 ^ (_s3 >> 19) ^ t ^ (t >> 8);
                return _s3;
            }

            /// <summary>
            /// A float in 0..1, both ends included, from the low 23 bits of one draw.
            /// </summary>
            public static float value => (NextUInt() & 0x7FFFFF) * (1f / 8388607f);

            /// <summary>
            /// A float between min and max, both included (one draw).
            /// </summary>
            public static float Range(float min, float max)
            {
                // Unity interpolates from max to min, not min to max.
                float t = value;
                return t * min + (1f - t) * max;
            }

            /// <summary>
            /// An int from min up to but not including max (one draw); min when they are equal. With min &gt; max it
            /// counts down from min, as Unity's does.
            /// </summary>
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

            /// <summary>
            /// A point in the unit disc (two draws: angle, then radius).
            /// </summary>
            public static Vector2 insideUnitCircle
            {
                get
                {
                    // Native engine code: single-precision trigonometry.
                    float angle = Range(0f, 1f) * (float)(Math.PI * 2.0);
                    float radius = MathF.Sqrt(Range(0f, 1f));
                    return new Vector2(radius * MathF.Cos(angle), radius * MathF.Sin(angle));
                }
            }
        }
    }
}
