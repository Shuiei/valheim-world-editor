using Avalonia.Threading;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// Searching random seeds in the background, and the best ones shown when done.
/// </summary>
public sealed partial class NewWorldPage : IDisposable
{
    /// <summary>
    /// Random seeds looked at in the background; the best shown when done (or stopped).
    /// </summary>
    internal void StartSearch()
    {
        _search?.Cancel();
        var cancel = _search = new CancellationTokenSource();
        var token = cancel.Token;
        int count = Count.SelectedIndex switch { 0 => 60, 2 => 400, _ => 150 };
        var wishes = Wishes();
        Find.IsEnabled = false;
        Stop.IsVisible = Progress.IsVisible = true;
        Progress.Value = 0;
        ClearFound();
        var rules = GameLocations.UseGameRules(GameLook.Bundles);
        Pending = Task.Run(async () =>
        {
            await rules.ConfigureAwait(false);
            return SeedPreview.Search(wishes, count, 12, 96, (done, total) => Dispatcher.UIThread.Post(() => Progress.Value = (double)done / total), token);
        })
            .ContinueWith(t =>
            {
                // The work is over: its source goes (a replaced one too).
                bool stopped = cancel.IsCancellationRequested;
                if (_search == cancel)
                {
                    _search = null;
                }
                cancel.Dispose();
                Find.IsEnabled = true;
                Stop.IsVisible = Progress.IsVisible = false;
                if (t.Status != TaskStatus.RanToCompletion)
                {
                    ClearFound();
                    Found.Children.Add(Ui.Hint(Lang.T("newWorld.error.search", t.Exception?.GetBaseException().Message)));
                    Console.WriteLine($"New world: the seed search failed: {t.Exception?.GetBaseException()}");
                    return;
                }
                Results = t.Result;
                Console.WriteLine($"New world: searched {count} seeds{(stopped ? " (stopped)" : "")}, best {(Results.Count > 0 ? $"{Results[0].Preview.Seed} (score {Results[0].Score:0.0})" : "none")}.");
                ShowResults();
            }, TaskScheduler.FromCurrentSynchronizationContext());
    }
}
