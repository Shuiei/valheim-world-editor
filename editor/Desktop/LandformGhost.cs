namespace TerrainEditor.Desktop;

/// <summary>
/// What the Landform tool shows under the pointer before a click: how far the landform reaches, its outline on the
/// ground, and the metres it would add, drawn as a mesh.
/// </summary>
/// <param name="Reach">How far from the click it reaches, in metres: the outline's radius when it is round, and the
/// preview's half width.</param>
/// <param name="Shape">The metres it adds at (x, z) metres east and north of the click (negative digs); null: no
/// preview (a formula with a mistake, or the tool is not in use).</param>
/// <param name="MustFit">Whether it only goes in where it fits whole in the open area (a mountain): the outline turns
/// red where it does not.</param>
/// <param name="Square">Its outline when it is not a circle of <paramref name="Reach"/> (a picture's square, turned):
/// the points of the outline around the click, given how many a circle would have; null for the circle.</param>
/// <param name="Least">The least change, in metres, the preview draws.</param>
internal sealed record LandformGhost(float Reach, Func<float, float, float>? Shape, bool MustFit = false, Func<int, List<(float X, float Z)>>? Square = null, float Least = 0.5f)
{
    /// <summary>No landform: a 16 m circle and no preview (the tool is not in use).</summary>
    public static LandformGhost None { get; } = new(16, null);

    /// <summary>The outline around the click: <see cref="Square"/>, or a circle of <see cref="Reach"/>.</summary>
    /// <param name="n">How many points a circle has.</param>
    public List<(float X, float Z)> Outline(int n) =>
        Square?.Invoke(n) ?? [.. Enumerable.Range(0, n).Select(i => (MathF.Cos(i * MathF.Tau / n) * Reach, MathF.Sin(i * MathF.Tau / n) * Reach))];
}
