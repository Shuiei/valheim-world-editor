using System.Globalization;
using TerrainEditor.Save;

/// <summary>The look check: the biomes and water around the middle of a seed's world (see Program.cs for the usage).</summary>
internal static class LookCheck
{
    /// <summary>Prints the biome shares and the share under water within the reach around the middle; returns the exit code.</summary>
    /// <param name="args">look, the seed name, then an optional reach in metres.</param>
    internal static int Run(string[] args)
    {
        float reach = args.Length > 2 ? float.Parse(args[2], CultureInfo.InvariantCulture) : 400f;
        var gen = Look(args[1]);
        var shares = new Dictionary<string, int>();
        int water = 0, n = 0;
        for (float z = -reach; z <= reach; z += 16f)
        {
            for (float x = -reach; x <= reach; x += 16f)
            {
                if (x * x + z * z > reach * reach)
                {
                    continue;
                }
                string b = gen.GetBiome(x, z).ToString();
                shares[b] = shares.GetValueOrDefault(b) + 1;
                water += gen.GetHeight(new ValheimGen.Vector2(x, z)) < ValheimGen.TerrainService.WaterLevel ? 1 : 0;
                n++;
            }
        }
        Console.WriteLine($"seed {args[1]}: middle {gen.GetBiome(0, 0)}, within {reach} m: " + string.Join(", ", shares.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key} {100.0 * kv.Value / n:0}%")) + $", under water {100.0 * water / n:0}%");
        return 0;
    }

    /// <summary>The world generator for a seed name, as a new world of this game version would have it (no save needed).</summary>
    /// <param name="seedName">The seed's name, as typed in the game.</param>
    private static ValheimGen.WorldGenerator Look(string seedName)
    {
        return ValheimGen.WorldGenerator.Create(new ValheimGen.World { m_seedName = seedName, m_seed = WorldCreator.SeedOf(seedName), m_worldGenVersion = WorldCreator.WorldGenVersion });
    }
}
