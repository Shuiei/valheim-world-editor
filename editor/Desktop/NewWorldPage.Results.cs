using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.VisualTree;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// The search results as pictures to click, and how they are taken away.
/// </summary>
public sealed partial class NewWorldPage : IDisposable
{
    /// <summary>
    /// The search results as pictures to click, each with its seed and score.
    /// </summary>
    private void ShowResults()
    {
        ClearFound();
        if (Results.Count == 0)
        {
            Found.Children.Add(Ui.Hint(Lang.T("newWorld.hint.noResults")));
            return;
        }
        foreach (var (p, score) in Results)
        {
            var b = new Button
            {
                Padding = new Thickness(3),
                Tag = p.Seed,
                Content = new StackPanel
                {
                    Spacing = 2,
                    Children =
                    {
                        new Image { Source = Draw(p, 120), Width = 120, Height = 120 },
                        new TextBlock { Text = p.Seed, FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center },
                        new TextBlock { Text = Lang.T("newWorld.score", score), FontSize = 10, Foreground = Ui.Muted, HorizontalAlignment = HorizontalAlignment.Center },
                    },
                },
            };
            ToolTip.SetTip(b, Describe(p.Stats));
            b.Click += (_, _) =>
            {
                // Looked at at once (no typing pause to wait for).
                SeedBox.Text = p.Seed;
                ShowSeed(p.Seed);
            };
            Found.Children.Add(b);
        }
        MarkFound(Shown?.Seed);
    }

    /// <summary>
    /// The search results taken away, their pictures freed.
    /// </summary>
    private void ClearFound()
    {
        foreach (var image in Found.GetVisualDescendants().OfType<Image>())
        {
            (image.Source as IDisposable)?.Dispose();
            image.Source = null;
        }
        Found.Children.Clear();
    }
}
