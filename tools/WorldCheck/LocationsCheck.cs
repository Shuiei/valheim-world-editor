using System.Globalization;
using ValheimGen;

/// <summary>
/// The locations check: every location the editor lays out for a seed against those the game laid out (the AltDump
/// test plugin's location file: one "L prefab x z" line each, x and z as float bits), as sets (see Program.cs for the
/// usage).
/// </summary>
internal static class LocationsCheck
{
    /// <summary>Prints the counts, and the kinds with instances on one side only; returns 0 when both sides are the same.</summary>
    /// <param name="args">locations, the dump, the seed name, then the Valheim folder (else the one the editor finds).</param>
    internal static int Run(string[] args)
    {
        if (GameRules.Use(args.Length > 3 ? args[3] : null) is null)
        {
            return 1;
        }
        var gen = GameRules.Generator(args[2]);
        var mine = SeedLocations.Place(gen, SeedLocations.Which.All, parallel: true)
            .Select(p => $"L {p.Rule.Prefab} {Bits(p.X)} {Bits(p.Z)}").ToHashSet(StringComparer.Ordinal);
        var game = File.ReadAllLines(args[1]).ToHashSet(StringComparer.Ordinal);
        var onlyGame = game.Except(mine).ToList();
        var onlyMine = mine.Except(game).ToList();
        Console.WriteLine($"game {game.Count}, editor {mine.Count}, both {game.Count - onlyGame.Count}");
        foreach (string kind in onlyGame.Select(Kind).Concat(onlyMine.Select(Kind)).Distinct().Order(StringComparer.Ordinal))
        {
            Console.WriteLine($"  {kind}: game only {onlyGame.Count(l => Kind(l) == kind)}, editor only {onlyMine.Count(l => Kind(l) == kind)}");
        }
        return onlyGame.Count == 0 && onlyMine.Count == 0 ? 0 : 1;
    }

    /// <summary>A line's prefab.</summary>
    private static string Kind(string line) => line.Split(' ')[1];

    /// <summary>A float's bits in hex.</summary>
    private static string Bits(float f) => BitConverter.SingleToInt32Bits(f).ToString("x8", CultureInfo.InvariantCulture);
}
