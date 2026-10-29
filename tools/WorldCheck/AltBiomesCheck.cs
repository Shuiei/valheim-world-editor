using ValheimGen;

/// <summary>
/// The alt biomes check: the editor's biome areas and alt biomes of a seed against those the game made (the AltDump
/// test plugin's file, see docs/development.md), line by line (see Program.cs for the usage).
/// </summary>
internal static class AltBiomesCheck
{
    /// <summary>Prints how many lines match and the first that differ; returns 0 when all match.</summary>
    /// <param name="args">altbiomes, the dump, the seed name, then the Valheim folder (else the one the editor finds).</param>
    internal static int Run(string[] args)
    {
        if (GameRules.Use(args.Length > 3 ? args[3] : null) is not { } rules)
        {
            return 1;
        }
        var gen = GameRules.Generator(args[2]);
        var mine = SeedLocations.AltBiomeLayout.DumpLines(new SeedLocations.BiomeMap(gen, parallel: true, CancellationToken.None), gen, rules.AltBiomes);
        // The game's first line also names the seed, which the editor's does not know.
        string[] game = [.. File.ReadAllLines(args[1]).Select(l => System.Text.RegularExpressions.Regex.Replace(l, "^(# seed \\S+) name \\S*", "$1"))];
        return Compare(game, mine);
    }

    /// <summary>Prints how many lines match and the first twenty that differ; returns 0 when all match.</summary>
    private static int Compare(string[] game, List<string> mine)
    {
        int lines = Math.Max(game.Length, mine.Count), same = 0, shown = 0;
        for (int i = 0; i < lines; i++)
        {
            string g = i < game.Length ? game[i] : "(none)", m = i < mine.Count ? mine[i] : "(none)";
            if (g == m)
            {
                same++;
            }
            else if (shown++ < 20)
            {
                Console.WriteLine($"line {i + 1}\n  game:   {g}\n  editor: {m}");
            }
        }
        Console.WriteLine($"{same} of {lines} lines the same.");
        return same == lines ? 0 : 1;
    }
}
