using TerrainEditor.App;
using TerrainEditor.Save;

namespace TerrainEditor.Desktop;

public sealed partial class WorldSession
{
    /// <summary>
    /// Live: the changed zones' ground and the object changes go into the running game; zone resets last
    /// (the game regenerates them), after which the world is read again from the game. The history goes across
    /// that, and across No limit ground made into ground discs (<see cref="TakeBackHistory"/>).
    /// </summary>
    /// <param name="confirm">Asked, with the zones, when applying would write over ground the game changed under
    /// the editor's changes (asked here, while no follow can come in between); null applies without asking.</param>
    public async Task<Outcome> ApplyLive(Func<IReadOnlyList<(int X, int Z)>, Task<bool>>? confirm = null)
    {
        await _applying.WaitAsync();
        Interlocked.Increment(ref _applies);
        try
        {
            if (confirm != null && Edits.PendingOverGame() is { Count: > 0 } over && !await confirm(over))
            {
                return new Outcome(false, Said.Of("save.msg.declined"), false) { Declined = true };
            }
            // Zone resets: the world is read again from the game, from which the history's objects cannot be read; it is
            // taken now. With only ground discs, it is taken once they are in (steps made meanwhile with it), on the
            // generated ground as it was before them.
            var resets = Edits.Resets;
            var carried = resets.Count > 0 ? CarryHistory() : null;
            _applyLift = null;
            var o = await ApplyLiveOnce();
            if (!o.Reloaded && o.Lifted && _applyLift is { } lift)
            {
                carried = CarryHistory(lift);
            }
            if (o.Reloaded || o.Lifted)
            {
                // The resets the game made: those not waiting any more.
                var waiting = Edits.Resets.Select(r => (r.X, r.Z)).ToHashSet();
                TakeBackHistory(carried, [.. resets.Where(r => !waiting.Contains((r.X, r.Z)))], o.Done, o.Unsent == LiveParts.None ? null : o.Sent, later: true);
            }
            return o;
        }
        finally
        {
            Interlocked.Increment(ref _applies);
            _applying.Release();
        }
    }

    /// <summary>
    /// A failure of the ground call that shows the game took none of it: no connection could be made, or the plugin
    /// turned it down before reading it (4xx or 503). A 500, or an answer broken off or too late, may come after the
    /// game took the ground.
    /// </summary>
    private static bool NothingSent(Exception ex) =>
        ex is HttpRequestException { HttpRequestError: HttpRequestError.ConnectionError or HttpRequestError.NameResolutionError }
            or BridgeAnswerException { Status: (>= 400 and < 500) or 503 };

    /// <summary>The game unreachable or answering with an error (InvalidOperationException from the bridge).</summary>
    private static bool GameFailed(Exception ex) =>
        ex is HttpRequestException or IOException or TaskCanceledException or InvalidDataException or InvalidOperationException or System.Text.Json.JsonException;
}
