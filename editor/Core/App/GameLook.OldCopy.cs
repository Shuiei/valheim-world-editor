// The removal of the copies older editors made of the game's look (see GameLook).

namespace TerrainEditor.App;

public static partial class GameLook
{
    /// <summary>
    /// The copy older editors made (up to 1.16): about 150 MB nothing reads any more. Called by the app
    /// at start (not by the tests' editors: their data folder is the user's).
    /// </summary>
    public static void DeleteOldCopy()
    {
        foreach (string d in new[] { Dir, OldCache })
        {
            try
            {
                if (Directory.Exists(d))
                {
                    Directory.Delete(d, true);
                    Console.WriteLine($"game look: deleted the old copy in {d}");
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                Log.Caught(e, "deleting the game look's old copy (left)");
            }
        }
    }
}
