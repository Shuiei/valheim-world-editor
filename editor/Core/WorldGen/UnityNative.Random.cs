// Unity's UnityEngine.Random, re-implemented (see UnityNative.cs for the engine functions this file covers).
namespace ValheimGen
{
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
            public struct RandomState : IEquatable<RandomState>
            {
                /// <summary>
                /// First state word.
                /// </summary>
                internal uint S0;

                /// <summary>
                /// Second state word.
                /// </summary>
                internal uint S1;

                /// <summary>
                /// Third state word.
                /// </summary>
                internal uint S2;

                /// <summary>
                /// Fourth state word.
                /// </summary>
                internal uint S3;

                /// <summary>Equal when the four state words are equal.</summary>
                public readonly bool Equals(RandomState other) => S0 == other.S0 && S1 == other.S1 && S2 == other.S2 && S3 == other.S3;

                /// <summary>Equal to another state with the same four words.</summary>
                public override readonly bool Equals(object? obj) => obj is RandomState v && Equals(v);

                /// <summary>Whether the two states hold the same four words.</summary>
                public static bool operator ==(RandomState left, RandomState right) => left.Equals(right);

                /// <summary>Whether the states differ in a word.</summary>
                public static bool operator !=(RandomState left, RandomState right) => !left.Equals(right);

                /// <summary>A hash of the four words, equal for equal states.</summary>
                public override readonly int GetHashCode() => HashCode.Combine(S0, S1, S2, S3);
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
            public static RandomState State
            {
                get => new() { S0 = _s0, S1 = _s1, S2 = _s2, S3 = _s3 };
                set
                {
                    _s0 = value.S0;
                    _s1 = value.S1;
                    _s2 = value.S2;
                    _s3 = value.S3;
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
            public static float Value => (NextUInt() & 0x7FFFFF) * (1f / 8388607f);

            /// <summary>
            /// A float between min and max, both included (one draw).
            /// </summary>
            public static float Range(float min, float max)
            {
                // Unity interpolates from max to min, not min to max.
                float t = Value;
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
            internal static Vector2 InsideUnitCircle
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
