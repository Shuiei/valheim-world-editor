namespace WorldEditor.Tests;

/// <summary>
/// The world creator tests' shared set-up: a temporary folder per test (removed afterwards) and the test world's seed.
/// </summary>
public abstract class WorldCreatorTestBase : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "vwe-newworld-" + Guid.NewGuid().ToString("N")[..8]);

    // The test world's seed (tests/fixtures/CITest).
    protected const string Seed = "GbXfypqU3G";

    /// <summary>
    /// Deletes the temporary folder.
    /// </summary>
    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, true);
        }
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// A folder inside the temporary folder.
    /// </summary>
    protected string Folder(string name) => Path.Combine(_dir, name);
}
