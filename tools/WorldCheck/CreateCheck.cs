using System.Globalization;
using TerrainEditor.App;
using TerrainEditor.Save;

/// <summary>The create check: a new world from a seed, with its middle generated (see Program.cs for the usage).</summary>
internal static class CreateCheck
{
    /// <summary>Creates the world that the arguments name and prints what was made; returns the exit code.</summary>
    /// <param name="args">create, the folder, the name, the seed, then an optional radius and --flat.</param>
    internal static int Run(string[] args)
    {
        if (args.Length < 4)
        {
            Console.Error.WriteLine("create needs <folder> <name> <seed> [radius] [--flat]");
            return 2;
        }
        // The game's prefabs and location prefabs, for the new world's objects, and its location rules, for where they go.
        var game = GameBundles.ForGame(GameLook.FindValheim(AppSettings.Load().ValheimPath), GameLook.IndexFile);
        if (!GameData.Ensure(game))
        {
            Console.Error.WriteLine(GameData.Missing);
            return 1;
        }
        GameLocations.UseGameRules(game).Wait();
        try
        {
            int radius = args.Length > 4 && args[4] != "--flat" ? int.Parse(args[4], CultureInfo.InvariantCulture) : 0;
            var made = WorldCreator.Create(args[1], args[2], args[3], radius, flat: args.Contains("--flat"));
            Console.WriteLine($"Created '{args[2]}' (seed {args[3]} = {made.Seed}) in {made.Directory}: {made.Zones} zones generated, {made.Objects:N0} objects, {made.Locations} locations."
                + (made.Flat is var (fx, fz) ? $" Zone {fx}, {fz}: the most even dry Meadows ({made.FlatSpan:0.0} m from lowest to highest), left bare." : ""));
            return 0;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or FormatException)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }
}
