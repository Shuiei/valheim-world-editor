using Avalonia;
using Avalonia.Controls.Primitives;
using TerrainEditor.App;
using TerrainEditor.Editing;

namespace TerrainEditor.Desktop;

/// <summary>
/// Making the dungeon for the settings shown, away from the window's thread, and showing what was made.
/// </summary>
public sealed partial class DungeonGenPanel
{
    /// <summary>Makes the dungeon for the settings shown, away from the window's thread (see <see cref="Pending"/>).</summary>
    private void Make()
    {
        _wait.Stop();
        if (!GameDataReady())
        {
            Summary.Text = Lang.T("dungeon.hint.waitingForGame");
            _wait.Start();
            return;
        }
        var s = Settings();
        int generation = ++_generation;
        Summary.Text = Lang.T("dungeon.hint.making");
        Pending = Task.Run<(DungeonGen.Result? Result, string? Error)>(() =>
        {
            try
            {
                return (Result: DungeonGen.Make(s), Error: null);
            }
            catch (Exception e)
            {
                Log.Caught(e, "making a dungeon");
                return (Result: null, Error: e.Message);
            }
        }).ContinueWith(t =>
        {
            if (generation == _generation)
            {
                Show(t.Result.Result, t.Result.Error);
            }
        }, TaskScheduler.FromCurrentSynchronizationContext());
    }

    /// <summary>Shows a dungeon made (or why not): its summary, the level buttons and the plan.</summary>
    private void Show(DungeonGen.Result? result, string? error)
    {
        _result = result;
        Summary.Text = result != null ? Said.Join(" ", result.Notes).Shown : Lang.T("dungeon.hint.notMade", error);
        Place.IsEnabled = OpenOnPlot.IsEnabled = AddToPlot.IsEnabled = _result != null;
        var levels = _result?.Map?.Select(r => r.Level).Distinct().Order().ToList() ?? [];
        _level = Math.Clamp(_level, 0, Math.Max(0, levels.Count - 1));
        _levelButtons.Children.Clear();
        foreach (int l in levels)
        {
            var b = new ToggleButton { Content = Lang.T("dungeon.label.level", l + 1), FontSize = 11, IsChecked = l == _level, Padding = new Thickness(6, 2) };
            b.Tip("dungeon.gen.level");
            int at = l;
            b.Click += (_, _) =>
            {
                _level = at;
                DrawPlan();
                foreach (var c in _levelButtons.Children.OfType<ToggleButton>())
                {
                    c.IsChecked = c == b;
                }
            };
            _levelButtons.Children.Add(b);
        }
        _levelButtons.IsVisible = levels.Count > 1;
        DrawPlan();
    }
}
