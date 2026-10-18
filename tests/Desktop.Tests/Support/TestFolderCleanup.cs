namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Deletes the folders the Desktop tests make for themselves, once a test is done with them.
/// </summary>
internal static class TestFolderCleanup
{
    /// <summary>
    /// Deletes each of these folders, with what is in it, where it exists.
    /// </summary>
    /// <param name="folders">The folders to delete.</param>
    internal static void DeleteIfExists(params string[] folders)
    {
        foreach (string dir in folders)
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, true);
            }
        }
    }
}
