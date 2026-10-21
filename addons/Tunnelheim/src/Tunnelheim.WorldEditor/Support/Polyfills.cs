// What netstandard2.0 lacks of the newer .NET this code is written for.
namespace System.Runtime.CompilerServices
{
    /// <summary>Lets records and init-only setters compile for netstandard2.0.</summary>
    internal static class IsExternalInit;
}

namespace Tunnelheim.WorldEditor
{
    /// <summary>The float maths netstandard2.0 lacks: each worked out as Math does, rounded to float once.</summary>
    internal static class FloatMath
    {
        /// <summary>The absolute value.</summary>
        public static float Abs(float v) => Math.Abs(v);

        /// <summary>The larger of two (NaN when either is).</summary>
        public static float Max(float a, float b) => Math.Max(a, b);

        /// <summary>The sine of an angle in radians.</summary>
        public static float Sin(float v) => (float)Math.Sin(v);

        /// <summary>The cosine of an angle in radians.</summary>
        public static float Cos(float v) => (float)Math.Cos(v);
    }
}
