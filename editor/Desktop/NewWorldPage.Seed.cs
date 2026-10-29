using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// Showing a seed: its preview is made away from the window's thread, and only the newest is shown.
/// </summary>
public sealed partial class NewWorldPage : IDisposable
{
    /// <summary>
    /// The seed's preview, made away from the window's thread (only the newest is shown).
    /// </summary>
    internal void ShowSeed(string seed)
    {
        _preview?.Cancel();
        Problem.Text = "";
        _generated = seed;
        FitGenerate();
        if (seed.Length is 0 or > 10)
        {
            Note(Lang.T("newWorld.error.seedLength"));
            // Nothing shown: the last seed's dungeon search stops and its marks go (their controls are off the canvas).
            StopDungeons();
            SetLoading(null);
            Map.Clear();
            Marks.Children.Clear();
            _marks.Clear();
            Shown = null;
            FillDungeons();
            return;
        }
        var cancel = _preview = new CancellationTokenSource();
        // Read now: the source is disposed once the work ends (or the page goes), not while it reads it.
        var token = cancel.Token;
        StopDungeons();
        SetLoading(seed);
        if (Shown == null)
        {
            Note(Lang.T("newWorld.msg.looking"));
        }
        // The game's own location rules first (read once, the first time in a few seconds).
        var rules = GameLocations.UseGameRules(GameLook.Bundles);
        Pending = Task.Run(async () =>
        {
            await rules.ConfigureAwait(false);
            RefreshRules();
            return SeedPreview.Make(seed, _previewSize, parallel: true, landmarks: true, Rules, token);
        }, token).ContinueWith(t =>
        {
            // The work is over: its source goes (a replaced one too).
            bool stopped = cancel.IsCancellationRequested;
            if (_preview == cancel)
            {
                _preview = null;
            }
            cancel.Dispose();
            // A newer seed asked for since: that one's answer will come.
            if (stopped)
            {
                return;
            }
            SetLoading(null);
            if (t.Status != TaskStatus.RanToCompletion)
            {
                Note(Lang.T("newWorld.error.seedFailed", t.Exception?.GetBaseException().Message));
                Console.WriteLine($"New world: seed {seed} could not be looked at: {t.Exception?.GetBaseException()}");
                // Not shown: Generate can try it again.
                _generated = Shown?.Seed;
                FitGenerate();
                return;
            }
            Show(t.Result);
        }, TaskScheduler.FromCurrentSynchronizationContext());
    }
}
