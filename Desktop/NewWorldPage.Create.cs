using TerrainEditor.App;
using TerrainEditor.Save;

namespace TerrainEditor.Desktop;

/// <summary>
/// Create: the world's name and seed are checked, then the window makes the world.
/// </summary>
public sealed partial class NewWorldPage : IDisposable
{
    /// <summary>
    /// Create: checked here, made by the window (CreateRequested).
    /// </summary>
    internal void AskCreate()
    {
        string name = (NameBox.Text ?? "").Trim(), seed = (SeedBox.Text ?? "").Trim();
        var roots = (List<string>)Where.Tag!;
        string root = roots[Math.Max(0, Where.SelectedIndex)];
        if (Check(root, name, seed) is string problem)
        {
            Problem.Text = problem;
            return;
        }
        Problem.Text = "";
        StopAll();
        CreateRequested?.Invoke(Path.Combine(root, name), name, seed);
    }

    /// <summary>
    /// Why a world of that name and seed cannot be made in that worlds folder (a name that cannot be a file name on
    /// every system, a seed not of 1 to 10 characters, a world of that name there already), or null when it can.
    /// </summary>
    /// <param name="root">The worlds folder.</param>
    /// <param name="name">The world's name, trimmed.</param>
    /// <param name="seed">The seed, trimmed.</param>
    /// <param name="english">In English (what Claude is told) rather than the language in use (the page's).</param>
    internal static string? Check(string root, string name, string seed, bool english = false)
    {
        if (WhyNot(root, name, seed) is not { } problem)
        {
            return null;
        }
        return english ? problem.English : problem.Shown;
    }

    /// <summary>
    /// Why a world cannot be made with this name and seed in the worlds folder, in both languages; null when it can.
    /// </summary>
    /// <param name="root">The worlds folder.</param>
    /// <param name="name">The world's name, trimmed.</param>
    /// <param name="seed">The seed, trimmed.</param>
    private static Said? WhyNot(string root, string name, string seed)
    {
        if (!WorldCreator.IsWorldName(name))
        {
            return Said.Of("newWorld.error.name");
        }
        if (seed.Length is 0 or > 10)
        {
            return Said.Of("newWorld.error.seedLength");
        }
        if (Directory.Exists(Path.Combine(root, name)) || File.Exists(Path.Combine(root, name + ".fwl")))
        {
            return Said.Of("newWorld.error.exists", name, Ui.Tilde(root));
        }
        return null;
    }
}
