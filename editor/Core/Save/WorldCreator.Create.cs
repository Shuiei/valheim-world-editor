
namespace TerrainEditor.Save;

public static partial class WorldCreator
{
    /// <summary>Writes a new world into a folder that holds none yet.</summary>
    /// <param name="folder">The folder to write into; created when missing.</param>
    /// <param name="name">The world's name, which must also be a file name on every system (<c>IsWorldName</c>).</param>
    /// <param name="seedName">The seed, 1 to 10 characters as in the game.</param>
    /// <param name="radius">
    /// How many zones from the middle (0 to <c>MaxRadius</c>) are generated with vegetation; 0 leaves every zone to the
    /// game.
    /// </param>
    /// <param name="uid">The world's unique id, or null for a random one.</param>
    /// <param name="flat">
    /// The most even zone of dry Meadows left without vegetation, its ground untouched (no edit, so the tools keep
    /// their whole ±8 m on it): a clean square to show them on (the documentation's pictures). Needs a radius of 2 or
    /// more.
    /// </param>
    /// <param name="rules">
    /// What a world generation mod changes (the generated zones' ground, vegetation and locations follow it); null for
    /// the game's own values. The world file gets no settings of the mod's: Expand World Size writes its own on the
    /// first save.
    /// </param>
    /// <exception cref="ArgumentException">A bad name, seed, radius or flat request.</exception>
    /// <exception cref="InvalidOperationException">
    /// The folder already holds a world, or no zone is fit to be the flat one.
    /// </exception>
    public static Created Create(string folder, string name, string seedName, int radius = 0, long? uid = null, bool flat = false, ValheimGen.WorldRules? rules = null)
    {
        name = name.Trim();
        seedName = seedName.Trim();
        CheckRequest(name, seedName, radius, flat);
        RefuseExistingWorld(folder);
        // What is written is taken away again should anything fail (no zone fit to be the flat one, a full disk): a
        // part-written world would stop the next try in this folder.
        bool madeFolder = !Directory.Exists(folder);
        Directory.CreateDirectory(folder);
        var written = new List<string>();
        try
        {
            int seed = SeedOf(seedName);
            string main = Path.Combine(folder, "_main.1");
            written.AddRange([main + ".fwl2", main + ".chunks", main + ".db2"]);
            WriteMetadata(main + ".fwl2", name, seedName, seed, uid ?? Random.Shared.NextInt64(1, long.MaxValue), rules?.Generator?.Header as App.GeneratorHeader);
            WorldWriter.WriteIndex(main + ".chunks", SaveFileVersion, []);
            var db = ZoneDb.Empty(SaveFileVersion, LocationVersion, Tail());
            db.Save(main + ".db2");

            var growth = radius > 0 ? GrowVegetation(folder, main, db, written, seed, radius, flat, rules ?? ValheimGen.WorldRules.Vanilla) : new Growth(0, 0, 0, null);
            // Written last, like the game: only then is the save complete. Every file and its name are on disk before it
            // (a crash must not leave a marker vouching for files that are not), the marker's name after it.
            written.Add(main + ".ok");
            DirectorySync.Flush(folder);
            WorldWriter.WriteAllBytesDurable(main + ".ok", BitConverter.GetBytes(SaveFileVersion));
            DirectorySync.Flush(folder);
            var flatZone = growth.FlatZone;
            return new Created(folder, seed, growth.Zones, growth.Objects, flatZone is var (cx, cz, _) ? (cx, cz) : null, flatZone?.Span ?? 0, growth.Placed);
        }
        catch
        {
            Unwrite(folder, written, madeFolder);
            throw;
        }
    }

    /// <summary>Checks the name, seed, radius and flat request; a bad one is refused before anything is written.</summary>
    /// <param name="name">The trimmed world name.</param>
    /// <param name="seedName">The trimmed seed.</param>
    /// <param name="radius">The generated radius in zones.</param>
    /// <param name="flat">Whether a bare zone is asked for.</param>
    internal static void CheckRequest(string name, string seedName, int radius, bool flat)
    {
        if (!IsWorldName(name))
        {
            throw new ArgumentException("The world needs a name that can be a file name on every system.", nameof(name));
        }
        if (seedName.Length is 0 or > 10)
        {
            throw new ArgumentException("The seed is 1 to 10 characters, as in the game.", nameof(seedName));
        }
        if (radius is < 0 or > MaxRadius)
        {
            throw new ArgumentOutOfRangeException(nameof(radius), $"The generated zones reach 0 to {MaxRadius} zones from the middle.");
        }
        if (flat && radius < 2)
        {
            throw new ArgumentException("A bare zone needs generated zones around it (a radius of 2 or more).", nameof(flat));
        }
    }

    /// <summary>Refuses a folder that already holds a world: a new one is never written over it.</summary>
    /// <param name="folder">The folder to write into.</param>
    private static void RefuseExistingWorld(string folder)
    {
        if (Directory.Exists(folder) && Directory.EnumerateFileSystemEntries(folder).Any(f => Path.GetFileName(f).StartsWith("_main.") || f.EndsWith(".chunk")))
        {
            throw new InvalidOperationException($"{folder} already holds a world: a new one is never written over it.");
        }
    }
}
