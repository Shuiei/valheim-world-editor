using WorldEditor.Tests;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// A copy of the test world for an editor process to open: its folder CITest in a new temporary folder of its own,
/// deleted again when the process is done.
/// </summary>
internal static class TestWorldCopy
{
    /// <summary>
    /// Copies the test world's files into a new folder named with the prefix and a random suffix, under the temporary
    /// folder, and returns that new folder; the world is its CITest folder.
    /// </summary>
    internal static string Make(string prefix)
    {
        string root = Path.Combine(Path.GetTempPath(), prefix + Guid.NewGuid().ToString("N")[..8]);
        CopyInto(root);
        return root;
    }

    /// <summary>
    /// Copies the test world's files into the folder CITest under the root (made if needed), and returns that folder.
    /// </summary>
    internal static string CopyInto(string root)
    {
        string world = Path.Combine(root, "CITest");
        Directory.CreateDirectory(world);
        foreach (string f in Directory.GetFiles(Path.Combine(Fixtures.Root, "CITest")))
        {
            File.Copy(f, Path.Combine(world, Path.GetFileName(f)));
        }
        return world;
    }

    /// <summary>
    /// Deletes a copy made by <see cref="Make"/>, if it was made.
    /// </summary>
    internal static void Remove(string? root)
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root!, true);
        }
    }
}
