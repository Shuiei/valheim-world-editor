using TerrainEditor.App;

namespace TerrainEditor.Save;

public static partial class WorldCreator
{
    /// <summary>
    /// Takes away the files a failed <see cref="Create"/> wrote, and the folder when it made it and nothing else is in
    /// it. A file that cannot be taken away is said (the failure itself is what the caller is told).
    /// </summary>
    private static void Unwrite(string folder, List<string> written, bool madeFolder)
    {
        try
        {
            foreach (string file in written.Distinct())
            {
                File.Delete(file);
            }
            if (madeFolder && !Directory.EnumerateFileSystemEntries(folder).Any())
            {
                Directory.Delete(folder);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Log.Caught(e, "taking away what a new world that failed had written (some is left)");
        }
    }

    /// <summary>Prefab hash of the game's zone control object (one in every generated zone).</summary>
    private static readonly int ZoneCtrl = StableHash.Of("_ZoneCtrl");

    /// <summary>Int data key of a location proxy's seed.</summary>
    private static readonly int SeedKey = StableHash.Of("seed");
}
