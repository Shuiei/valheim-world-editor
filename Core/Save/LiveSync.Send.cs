using TerrainEditor.App;

namespace TerrainEditor.Save;

public sealed partial class LiveSync
{
    /// <summary>
    /// Sends a call and records what the game did. A call with an id left without an answer is kept, to be sent again
    /// first next time; one the plugin answered with an error was not done, and is planned again.
    /// </summary>
    /// <returns>A one-line summary for the user, in both languages.</returns>
    private async Task<Said> Send(WorldSave world, LiveBridge live, Call call)
    {
        string reply;
        try
        {
            reply = await live.ApplyObjects(call.Destroy, call.Create, call.Moves, call.Id);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or IOException)
        {
            lock (_lock)
            {
                _unanswered = call.Id != null ? call : null;
            }
            throw;
        }
        catch (InvalidOperationException)
        {
            lock (_lock)
            {
                _unanswered = null;
            }
            throw;
        }
        lock (_lock)
        {
            _unanswered = null;
        }
        return Record(world, call, reply);
    }
}
