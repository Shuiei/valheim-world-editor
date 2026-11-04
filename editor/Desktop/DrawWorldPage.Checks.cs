using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// The checks beside the drawing: what the drawing alone tells (MasterMapChecks.OfMap), worked out again a moment
/// after each change; and what the world made of it tells (MasterMapChecks.OfWorld), once the World view has laid out
/// its places and then its dungeons.
/// </summary>
public sealed partial class DrawWorldPage
{
    /// <summary>The checks' lines, under their heading in the World card.</summary>
    internal StackPanel Checks { get; } = new() { Spacing = 4 };

    /// <summary>The drawing's checks and the world's, as last worked out (null: not yet, or out of date).</summary>
    private List<Said>? _mapChecks, _worldChecks;

    /// <summary>Works the drawing's checks out again 400 ms after the last change.</summary>
    private DispatcherTimer? _checkWait;

    /// <summary>Cancels the dungeons being laid out for the world's checks.</summary>
    private CancellationTokenSource? _dungeons;

    /// <summary>The latest checks worked out, for tests to wait on.</summary>
    internal Task CheckPending { get; private set; } = Task.CompletedTask;

    /// <summary>The drawing changed: its checks worked out again soon, the world's dropped until the World view is made again.</summary>
    private void ScheduleChecks()
    {
        _worldChecks = null;
        _dungeons?.Cancel();
        if (_checkWait == null)
        {
            _checkWait = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
            _checkWait.Tick += (_, _) =>
            {
                _checkWait.Stop();
                var map = Canvas.Map.Copy();
                CheckPending = Task.Run(() => MasterMapChecks.OfMap(map)).ContinueWith(t => Dispatcher.UIThread.Post(() =>
                {
                    if (t.IsFaulted)
                    {
                        Log.Caught(t.Exception!.GetBaseException(), "checking a master map");
                        return;
                    }
                    _mapChecks = t.Result;
                    ShowChecks();
                }), TaskScheduler.Default);
            };
        }
        _checkWait.Stop();
        _checkWait.Start();
    }

    /// <summary>
    /// The world's checks, once its preview is made: its dungeons and Fuling villages laid out on from its places (a few
    /// seconds), then what the game cannot lay out, or lays out few of.
    /// </summary>
    private void CheckWorld(SeedPreview.Preview p)
    {
        _dungeons?.Cancel();
        if (p.Layout is not { } layout || !ValheimGen.SeedLocations.Available)
        {
            return;
        }
        var cancel = _dungeons = new CancellationTokenSource();
        var prefabs = SeedPreview.Dungeons.SelectMany(d => d.Prefabs).Append(MasterMapChecks.FulingVillage).ToHashSet();
        Checks.Children.Add(Ui.Hint(Lang.T("drawWorld.msg.checkingDungeons")));
        CheckPending = Task.Run(() =>
        {
            var placed = layout.PlaceKinds(prefabs, cancel.Token);
            var counts = placed.GroupBy(x => x.Rule.Prefab).Select(g => (g.Key, g.Count())).ToList();
            return MasterMapChecks.OfWorld(counts, ValheimGen.SeedLocations.Current);
        }, cancel.Token).ContinueWith(t => Dispatcher.UIThread.Post(() =>
        {
            if (cancel.IsCancellationRequested)
            {
                return;
            }
            if (t.IsFaulted)
            {
                Log.Caught(t.Exception!.GetBaseException(), "laying out a drawn world's dungeons");
                return;
            }
            _worldChecks = t.Result;
            ShowChecks();
        }), TaskScheduler.Default);
    }

    /// <summary>The checks' lines: each warning, or that nothing is missing.</summary>
    private void ShowChecks()
    {
        Checks.Children.Clear();
        var all = (_mapChecks ?? []).Concat(_worldChecks ?? []).ToList();
        if (all.Count == 0)
        {
            Checks.Children.Add(new TextBlock { Text = Lang.T("drawWorld.msg.checksOk"), FontSize = 12, Foreground = Ui.Ok, TextWrapping = TextWrapping.Wrap });
            return;
        }
        foreach (var s in all)
        {
            Checks.Children.Add(new TextBlock { Text = "• " + s.Shown, FontSize = 12, Foreground = Ui.Warn, TextWrapping = TextWrapping.Wrap });
        }
    }
}
