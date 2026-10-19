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

    /// <summary>
    /// Mirrors World.LoadWorld: an int length prefix, then a ZPackage with version, names, seed, the generator version
    /// (26 and later), whether the world needs its database (30 and later) and the starting global keys (32 and
    /// later; what comes after them, the players seen, is not read).
    /// </summary>
    private void LoadMetadata()
    {
        string path = Path.Combine(Directory, $"_main.{SaveNumber}.fwl2");
        using ValheimReader file = new(File.OpenRead(path));
        int length = file.ReadInt();
        using ValheimReader pkg = new(new MemoryStream(file.ReadBytes(length)));
        int version = pkg.ReadInt();
        Name = pkg.ReadString();
        SeedName = pkg.ReadString();
        Seed = pkg.ReadInt();
        pkg.ReadLong();
        WorldGenVersion = version >= 26 ? pkg.ReadInt() : 0;
        if (version >= 30)
        {
            pkg.ReadBool();
        }
        if (version >= 32)
        {
            int keys = pkg.ReadCount(1);
            for (int i = 0; i < keys; i++)
            {
                StartingGlobalKeys.Add(pkg.ReadString());
            }
        }
        GridWidth = ExpandWorld.SavedGridWidth(StartingGlobalKeys, Name);
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
