namespace WorldEditor.Tests;

/// <summary>Points to ask a world generator about, shared by the generator's tests.</summary>
internal static class GeneratorPoints
{
    /// <summary>
    /// Points on rings round the world's middle, from r0 up to r1 (step metres apart), at n angles.
    /// </summary>
    public static IEnumerable<(float X, float Z)> Rings(float r0, float r1, float step, int n) =>
        from a in Enumerable.Range(0, n)
        from r in Enumerable.Range(0, (int)((r1 - r0) / step)).Select(i => r0 + i * step)
        select (r * MathF.Cos(a * 2 * MathF.PI / n), r * MathF.Sin(a * 2 * MathF.PI / n));
}
