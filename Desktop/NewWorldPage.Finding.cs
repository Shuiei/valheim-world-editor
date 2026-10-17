using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// Looking for a seed's dungeons in the background, and waiting for them while a kind is switched on.
/// </summary>
public sealed partial class NewWorldPage : IDisposable
{
    /// <summary>
    /// A dungeon kind switched on before they are found: the loading sign until they are.
    /// </summary>
    private void WaitForDungeons(SeedPreview.Preview p)
    {
        if (_dungeons == null)
        {
            return;
        }
        _dungeonWait = true;
        Loading.IsVisible = true;
        LoadingText.Text = Lang.T("newWorld.msg.findingDungeonsOf", p.Seed);
    }

    /// <summary>
    /// The seed's dungeons looked for (a few seconds, on one core: the game's smaller locations come first in its
    /// order; what the preview laid out is gone on from), kept for the seed.
    /// </summary>
    private void FindDungeons(SeedPreview.Preview p)
    {
        StopDungeons();
        _dungeonError = null;
        if (p.Layout is not { } layout || p.Landmarks == null)
        {
            return;
        }
        var cancel = _dungeons = new CancellationTokenSource();
        var token = cancel.Token;
        FillDungeons();
        Task.Run(() => SeedPreview.FindDungeons(layout, token), token).ContinueWith(t =>
        {
            // The work is over: its source goes (StopDungeons only cancels it).
            bool stopped = cancel.IsCancellationRequested;
            if (_dungeons == cancel)
            {
                _dungeons = null;
            }
            cancel.Dispose();
            if (stopped || Shown != p)
            {
                return;
            }
            if (_dungeonWait)
            {
                _dungeonWait = false;
                Loading.IsVisible = false;
            }
            if (t.Status != TaskStatus.RanToCompletion)
            {
                // Said in the list, not kept: switching a kind on again tries again.
                _dungeonError = t.Exception?.GetBaseException().Message ?? Lang.T("newWorld.error.stopped");
                Console.WriteLine($"The dungeons of {p.Seed} could not be found: {t.Exception?.GetBaseException()}");
                FillDungeons();
                return;
            }
            _dungeonError = null;
            DungeonMarks = t.Result;
            Console.WriteLine($"New world: {DungeonMarks.Count} dungeons found for {p.Seed}.");
            if (_dungeonsSeen.Count >= 20)
            {
                _dungeonsSeen.Clear();
            }
            _dungeonsSeen[p.Seed] = DungeonMarks;
            FillDungeons();
            PlaceDots();
        }, TaskScheduler.FromCurrentSynchronizationContext());
    }
}
