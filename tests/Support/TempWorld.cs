using TerrainEditor.Save;

namespace WorldEditor.Tests;

/// <summary>
/// The test world: tests/fixtures/CITest, a fresh world made by a dedicated server and filled live through the editor
/// (ground edits, trees, rocks, bushes, pickables, a crop, floors, walls, chests). Every test works on its own copy, so
/// saves never touch the fixture.
/// </summary>
public sealed class TempWorld : IDisposable
{
    public string Dir { get; }

    public TempWorld()
    {
        Dir = Path.Combine(Path.GetTempPath(), "vwe-test-" + Guid.NewGuid().ToString("N")[..10], "CITest");
        CopyDir(Fixtures.World, Dir);
    }

    public WorldSave Load() => WorldSave.Load(Dir);

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path.GetDirectoryName(Dir)!, recursive: true);
        }
        catch
        {
            // Best effort: a folder that cannot be deleted is left for the OS to clean up.
        }
    }

    public static void CopyDir(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (string f in Directory.GetFiles(from))
        {
            File.Copy(f, Path.Combine(to, Path.GetFileName(f)));
        }
    }
}
