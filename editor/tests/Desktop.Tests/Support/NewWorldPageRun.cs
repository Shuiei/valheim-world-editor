using Avalonia.Controls;
using WorldEditor.Tests;

using static TerrainEditor.Desktop.Tests.NewWorldPageTests;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// A page in a shown window, with its own world folders (temporary, deleted when disposed), and the worlds it asked
/// to create.
/// </summary>
internal sealed class NewWorldPageRun : IDisposable
{
    public NewWorldPage P { get; }
    public Window W { get; }
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "vwe-newworld-" + Guid.NewGuid().ToString("N")[..8]);
    public List<(string Folder, string Name, string Seed)> Created { get; } = [];
    public int Back { get; private set; }

    public NewWorldPageRun()
    {
        Fixtures.UseLocations();
        Directory.CreateDirectory(Path.Combine(Root, "local"));
        Directory.CreateDirectory(Path.Combine(Root, "cloud"));
        // Made on the window's thread, as the app makes it: the random seed it shows first comes back there.
        NewWorldPage? page = null;
        OnUi(() => page = new NewWorldPage([Path.Combine(Root, "local"), Path.Combine(Root, "cloud")], previewSize: 64));
        P = page!;
        P.CreateRequested += (folder, name, seed) => Created.Add((folder, name, seed));
        P.BackRequested += () => Back++;
        W = new Window { Width = 1600, Height = 1000, Content = P.View };
        W.Show();
        Settle();
    }

    /// <summary>
    /// Shows this seed at once and waits until its world is shown (the seed box left alone).
    /// </summary>
    public async Task Look(string seed)
    {
        OnUi(() => P.ShowSeed(seed));
        await TestWindows.Until(() => P.Shown?.Seed == seed && !P.Loading.IsVisible, SeedWait);
        Settle();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        P.Dispose();
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, true);
        }
    }
}
