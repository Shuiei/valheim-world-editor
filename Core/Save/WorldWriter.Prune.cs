using TerrainEditor.App;

namespace TerrainEditor.Save;

public static partial class WorldWriter
{
    /// <summary>
    /// Why a save made from save number <c>known</c> would throw away a newer one in the folder (the game saved the
    /// world since it was read), or null when there is none. Only a complete save counts: one cut short long ago would
    /// block every save.
    /// </summary>
    public static Said? Newer(string dir, int known) => WorldSave.CommittedSave(dir) is int newest && newest > known
        ? Said.Of("save.writer.newer", newest)
        : null;

    /// <summary>The highest save number in a world folder (-1: none), as <see cref="WorldSave.SaveNumbers"/> reads them.</summary>
    public static int LatestNumber(string dir) => WorldSave.SaveNumbers(dir).Latest;

    /// <summary>
    /// The world folder down to its latest save: earlier save numbers' files and the chunk files its index does not
    /// list go (an open world's base, once it is left). A newer save without its commit marker (cut short, or the game
    /// writing it right now) is left alone, and so are the chunk files then; so are files named _main.&lt;something
    /// else&gt;.
    /// </summary>
    public static void Prune(string dir)
    {
        if (LatestNumber(dir) < 0)
        {
            return;
        }
        Prune(WorldSave.Load(dir));
    }

    /// <summary>
    /// <see cref="Prune(string)"/> for a world just read from its folder's latest save: its chunk list is what stays.
    /// </summary>
    public static void Prune(WorldSave now)
    {
        string dir = now.Directory;
        int latest = now.SaveNumber;
        var keep = now.Chunks.Select(c => c.FileName).ToHashSet();
        var old = Directory.GetFiles(dir, "_main.*").Where(f => Path.GetFileName(f).Split('.') is [_, var n, _]
            && int.TryParse(n, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int number) && number < latest);
        if (LatestNumber(dir) == latest)
        {
            old = old.Concat(Directory.GetFiles(dir, "*.chunk").Where(f => !keep.Contains(Path.GetFileName(f))));
        }
        RemoveAll([.. old]);
    }
}
