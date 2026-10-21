namespace TerrainEditor.Desktop;

/// <summary>
/// The seeded random numbers of the look's noise: the same generator and seeds as the web editor's look.js, so the same
/// seed gives the same numbers in both.
/// </summary>
public static class SeededRandom
{
    /// <summary>
    /// The next number in [0, 1) of the sequence that <paramref name="state"/> starts; advances the state one step.
    /// </summary>
    /// <param name="state">The generator's state, updated in place.</param>
    public static double NextUnit(ref uint state)
    {
        state += 0x6D2B79F5;
        uint t = state;
        t = (t ^ (t >> 15)) * (t | 1);
        t ^= t + (t ^ (t >> 7)) * (t | 61);
        return (t ^ (t >> 14)) / 4294967296.0;
    }
}
