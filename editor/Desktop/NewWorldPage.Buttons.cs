using Avalonia.Input;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// The page's buttons: rolling and generating a seed, creating the world, and finding and stopping the search.
/// </summary>
public sealed partial class NewWorldPage : IDisposable
{
    /// <summary>Wires the page's buttons and boxes: Roll, Generate, Create, Find and Stop, and the seed box.</summary>
    private void WireButtons()
    {
        Roll.Click += (_, _) =>
        {
            SeedBox.Text = SeedPreview.RandomSeed();
            ShowSeed(SeedBox.Text);
        };
        SeedBox.TextChanged += (_, _) => FitGenerate();
        SeedBox.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter && Generate.IsEnabled)
            {
                ShowSeed(SeedBox.Text?.Trim() ?? "");
            }
        };
        Generate.Click += (_, _) => ShowSeed(SeedBox.Text?.Trim() ?? "");
        Create.Click += (_, _) => AskCreate();
        Find.Click += (_, _) => StartSearch();
        Stop.Click += (_, _) => _search?.Cancel();
    }
}
