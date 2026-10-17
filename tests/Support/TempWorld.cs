using TerrainEditor.Save;

namespace WorldEditor.Tests;

/// <summary>
/// The test world: tests/fixtures/CITest, a fresh world made by a dedicated server and filled live through the editor
/// (ground edits, trees, rocks, bushes, pickables, a crop, floors, walls, chests), in save format 41. Every test works on
/// its own copy, so saves never touch the fixture.
/// </summary>
public sealed class TempWorld : IDisposable
{
    public string Dir { get; }

    public TempWorld() : this("CITest")
    {
    }

    /// <summary>A copy of the world fixture of that name in tests/fixtures ("CITest", "CITest42").</summary>
    public TempWorld(string fixture)
    {
        Dir = Path.Combine(Path.GetTempPath(), "vwe-test-" + Guid.NewGuid().ToString("N")[..10], fixture);
        CopyDir(Path.Combine(Fixtures.Root, fixture), Dir);
    }

    public WorldSave Load() => WorldSave.Load(Dir);

    public void Dispose()
    {
        string folder = Path.GetDirectoryName(Dir)!;
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, recursive: true);
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
