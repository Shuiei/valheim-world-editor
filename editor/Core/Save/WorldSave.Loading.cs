using System.Globalization;
using System.Numerics;
using TerrainEditor.App;

namespace TerrainEditor.Save;

public sealed partial class WorldSave
{
    /// <summary>
    /// Everything else that is not terrain data (trees, rocks, bushes, pickables...): prefab, position, rotation (Euler
    /// degrees) and scale (zero when the prefab's own scale applies).
    /// </summary>
    public List<(int Id, int Prefab, Vector3 Position, Vector3 Rotation, Vector3 Scale)> Objects { get; } = [];

    /// <summary>
    /// Loads the current save of a world folder (see <c>CommittedSave</c>); a .db2 that cannot be read leaves
    /// <c>Zones</c> null.
    /// </summary>
    /// <exception cref="InvalidDataException">The folder holds no chunked world save.</exception>
    public static WorldSave Load(string directory)
    {
        int saveNumber = FindLatestSave(directory);
        WorldSave save = new() { Directory = directory, SaveNumber = saveNumber };
        save.LoadMetadata();
        save.LoadChunks();
        try
        {
            save.Zones = ZoneDb.Load(Path.Combine(directory, $"_main.{saveNumber}.db2"));
        }
        catch (Exception ex)
        {
            Log.Caught(ex, "reading the zone list in the world's .db2 file (the world opens without it)");
        }
        return save;
    }

    /// <summary>The world file's head (the .fwl2), as <see cref="ReadMetadata(string)"/> reads it.</summary>
    /// <param name="Name">The world's name.</param>
    /// <param name="SeedName">The seed as typed.</param>
    /// <param name="Seed">The seed number.</param>
    /// <param name="WorldGenVersion">The game's world generator version the world was made with (0 before version 26).</param>
    /// <param name="StartingGlobalKeys">The starting global keys (none before version 32).</param>
    /// <param name="Generator">The header a world generation mod keeps in a world it made (before the file version); null
    /// for any other world.</param>
    public sealed record Metadata(string Name, string SeedName, int Seed, int WorldGenVersion, List<string> StartingGlobalKeys, GeneratorHeader? Generator = null);

    /// <summary>
    /// Mirrors World.LoadWorld: an int length prefix, then a ZPackage with version, names, seed, the generator version
    /// (26 and later), whether the world needs its database (30 and later) and the starting global keys (32 and
    /// later; what comes after them, the players seen, is not read). A world a world generation mod made has its
    /// generator's header first (<see cref="WorldGenerators.ReadVersion"/>); one whose generator no add-on in use draws
    /// throws <see cref="GeneratorHeaderException"/>.
    /// </summary>
    /// <param name="path">The world file (_main.n.fwl2).</param>
    public static Metadata ReadMetadata(string path)
    {
        using FileStream file = File.OpenRead(path);
        return ReadMetadata(file);
    }

    /// <summary>The same, from the world file's bytes (a live game's, served by its bridge).</summary>
    /// <param name="stream">The world file's contents, from its start.</param>
    public static Metadata ReadMetadata(Stream stream)
    {
        using ValheimReader file = new(stream);
        int length = file.ReadInt();
        using ValheimReader pkg = new(new MemoryStream(file.ReadBytes(length)));
        int version = WorldGenerators.ReadVersion(pkg, out var generator);
        string name = pkg.ReadString();
        string seedName = pkg.ReadString();
        int seed = pkg.ReadInt();
        pkg.ReadLong();
        int worldGenVersion = version >= 26 ? pkg.ReadInt() : 0;
        if (version >= 30)
        {
            pkg.ReadBool();
        }
        List<string> keys = [];
        if (version >= 32)
        {
            int count = pkg.ReadCount(1);
            for (int i = 0; i < count; i++)
            {
                keys.Add(pkg.ReadString());
            }
        }
        return new Metadata(name, seedName, seed, worldGenVersion, keys, generator);
    }

    /// <summary>Reads the world file's head into this save (<see cref="ReadMetadata(string)"/>).</summary>
    private void LoadMetadata()
    {
        var meta = ReadMetadata(Path.Combine(Directory, $"_main.{SaveNumber}.fwl2"));
        Name = meta.Name;
        SeedName = meta.SeedName;
        Seed = meta.Seed;
        WorldGenVersion = meta.WorldGenVersion;
        TakeKeys(meta);
    }

    /// <summary>
    /// Takes the world file's starting global keys (and the save grid they name) and its generator header from its
    /// head: a live world's, read from the game's own file, which keeps what a mod takes out of the loaded list.
    /// </summary>
    /// <param name="meta">The world file's head.</param>
    public void TakeKeys(Metadata meta)
    {
        Generator = meta.Generator;
        StartingGlobalKeys.Clear();
        StartingGlobalKeys.AddRange(meta.StartingGlobalKeys);
        GridWidth = SavedGridWidth(StartingGlobalKeys, Name);
    }

    /// <summary>The save number to load, as <c>CommittedSave</c> picks it.</summary>
    /// <exception cref="InvalidDataException">No save index is in the folder.</exception>
    private static int FindLatestSave(string directory)
    {
        int best = CommittedSave(directory);
        if (best < 0)
        {
            throw new InvalidDataException("No _main.<n>.chunks file found in " + directory + ". Is this a chunked (Deep North) world save?");
        }
        return best;
    }

    /// <summary>
    /// The game keeps rolling saves (_main.&lt;n&gt;.*); the current one is the highest number with an index file and
    /// the commit marker (.ok, written last: a save without it was cut short, or is being written right now). A folder
    /// where no save has the marker: the highest index. -1: none.
    /// </summary>
    public static int CommittedSave(string directory) => SaveNumbers(directory) is var (latest, committed) && committed >= 0 ? committed : latest;

    /// <summary>
    /// The save numbers of a world folder, by its index files (_main.&lt;n&gt;.chunks, n plain digits): the highest, and
    /// the highest with its commit marker (.ok); -1 for none. The one scan for <see cref="CommittedSave"/> and the
    /// writer's next number.
    /// </summary>
    public static (int Latest, int Committed) SaveNumbers(string directory)
    {
        int best = -1, committed = -1;
        foreach (string path in System.IO.Directory.GetFiles(directory, "_main.*.chunks"))
        {
            string[] parts = Path.GetFileName(path).Split('.');
            if (parts.Length == 3 && int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int n))
            {
                best = Math.Max(best, n);
                if (n > committed && File.Exists(Path.Combine(directory, $"_main.{n}.ok")))
                {
                    committed = n;
                }
            }
        }
        return (best, committed);
    }
}
