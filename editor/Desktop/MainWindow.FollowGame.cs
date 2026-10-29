using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class MainWindow
{
    /// <summary>
    /// Live: reads what changed in the game in the zones shown and merges it into the open area (the timer's tick).
    /// A game that does not answer is asked again at the next tick.
    /// </summary>
    internal async Task FollowGame()
    {
        if (_followingGame || Session is not { IsLive: true, Scene.Owner: { } owner } s || _gameTooOld == owner)
        {
            return;
        }
        _followingGame = true;
        try
        {
            if (_gameFetched == null && !s.Stroking)
            {
                _gameFetched = await owner.FetchGame(ZonesOf(s.Scene));
            }
            // Merged and shown at once, on this thread: nothing changes the world or the area in between.
            // The area open now gets it, whichever it is (taking it twice changes nothing).
            if (Session is not { Scene.Owner: { } now } open || now != owner)
            {
                _gameFetched = null;
                return;
            }
            if (_gameFetched is { } fetched && !open.Stroking && owner.MergeGame(fetched) is { } followed)
            {
                _gameFetched = null;
                if (followed.Any && open.TakeGameChanges(followed))
                {
                    SayFromGame(followed);
                }
            }
            // Changes of the editor's undone where the game changed the ground meanwhile: the game's now.
            else if (_gameFetched == null && !open.Stroking && owner.TakeWaitingGround() is { Count: > 0 } waiting)
            {
                open.TakeGameChanges(new WorldSession.Followed(new Save.WorldSave.Merged([], [], [], []), [], waiting, []));
                Msg("live.msg.groundUndone", waiting.Count);
            }
            UpdateSaveBar();
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("too old", StringComparison.Ordinal))
        {
            // An older plugin on the server: no following (Reload still reads the whole world again).
            Log.CaughtBrief(ex, "following the game's changes");
            _gameTooOld = owner;
            Msg("live.msg.bridgeTooOld");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException or InvalidDataException or InvalidOperationException or System.Text.Json.JsonException or FormatException)
        {
            // The game does not answer right now: asked again at the next tick (brief: it can fail every tick).
            Log.CaughtBrief(ex, "following the game's changes");
        }
        finally
        {
            _followingGame = false;
        }
    }
}
