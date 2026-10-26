using TerrainEditor.App;
using TerrainEditor.Editing;

namespace TerrainEditor.Save;

public sealed partial class LiveSync
{
    /// <summary>
    /// Sends the object changes since the last apply to the game in one call, and records what the game accepted
    /// (nothing when the call fails, so it is sent again next time). New objects of kinds the editor cannot write are
    /// skipped. With a bridge that moves objects (1.40.0 on), an object moved in the editor (a saved object deleted, with
    /// one copy of it elsewhere that differs only by where it stands and how it is turned: a move, or a cut then pasted)
    /// moves in the game under its own ZDOID, so what mods know of it holds; moved back the same way on undo. With a
    /// bridge that does a call once (1.45.3 on), a call whose answer was lost (the connection dropped or timed out after
    /// the game did it) is sent again first under the same id, and the game answers with what it did instead of doing it
    /// twice; the changes since follow in a call of their own. With a bridge that changes data in place (1.97.0 on), an
    /// object replaced by a changed copy of it (a mod's tool's change) takes the copy's data under its own ZDOID, so a mod
    /// that keeps its records from being destroyed keeps them. If the game started again since (another run of the
    /// plugin, which does not know what the last one did), the apply fails instead until the world is read again.
    /// </summary>
    /// <returns>A one-line summary for the user in both languages, empty when there was nothing to send.</returns>
    public async Task<Said> Apply(WorldSave world, EditStore edits, LiveBridge live)
    {
        List<Said> said = [];
        Call? unanswered;
        lock (_lock)
        {
            unanswered = _unanswered;
        }
        // Asked at each apply, and refused when it cannot be: a move sent as a destroy and a creation would give the
        // object a new id (mods' notes of it lost), and the plugin may have changed since the last apply.
        LiveBridge.Abilities plugin = await live.Ask();
        if (unanswered != null)
        {
            // The game started again since: the new run does not know what the last one did, so the call may be in
            // the world already (saved before the game stopped). Not sent again until the world is read again.
            if (plugin.Run != unanswered.Run)
            {
                throw new InvalidOperationException("the game started again after an apply whose answer was lost, so whether it was done is unknown: reload the world from the game before applying again, so no object is made twice");
            }
            said.Add(await Send(world, live, unanswered));
        }
        Call call = Plan(world, edits, plugin.CanMove, plugin.CanUpdate);
        if (!call.Empty)
        {
            if (plugin.CanCallOnce)
            {
                call.Id = Guid.NewGuid().ToString("N");
                call.Run = plugin.Run;
            }
            said.Add(await Send(world, live, call));
        }
        return Said.Join("; ", said);
    }
}
