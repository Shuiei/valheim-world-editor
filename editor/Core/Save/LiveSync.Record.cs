using System.Globalization;
using System.Text.Json;
using TerrainEditor.App;

namespace TerrainEditor.Save;

public sealed partial class LiveSync
{
    /// <summary>What the game did with a call, from its answer: recorded, and told in one line for the user.</summary>
    /// <param name="world">The world the ids are of.</param>
    /// <param name="call">The call answered.</param>
    /// <param name="reply">The plugin's JSON answer.</param>
    private Said Record(WorldSave world, Call call, string reply)
    {
        using var doc = JsonDocument.Parse(reply);
        var ids = doc.RootElement.GetProperty("created").EnumerateArray().Select(e => e.GetString()!.Split(':')).ToList();
        var moveReplies = doc.RootElement.TryGetProperty("moves", out var mv)
            ? mv.EnumerateArray().Select(e => (Id: e.GetProperty("id").GetString()!.Split(':'), InPlace: e.GetProperty("inPlace").GetBoolean())).ToList()
            : [];
        // Snapshot objects moved back, now followed under the ZDOID that came back.
        List<(int Id, (long User, uint Id) Zdo)> movedBack = [];
        lock (_lock)
        {
            foreach (int id in call.DestroyIds)
            {
                _destroyed.Add(id);
                DropLive(id);
            }
            foreach (int id in call.RestoreIds)
            {
                _destroyed.Remove(id);
                // Made again on its own: a copy it was moved to is a copy of its own from now on.
                _movedTo.Remove(id);
            }
            foreach (int id in call.DropNew)
            {
                DropLive(id);
                ForgetMovesTo(id);
            }
            for (int i = 0; i < ids.Count && i < call.CreateFor.Count; i++)
            {
                SetLive(call.CreateFor[i], new LiveTarget(long.Parse(ids[i][0], CultureInfo.InvariantCulture), uint.Parse(ids[i][1], CultureInfo.InvariantCulture), call.CreateWhat[i].Prefab, call.CreateWhat[i].Position));
            }
            for (int i = 0; i < moveReplies.Count && i < call.MoveFor.Count; i++)
            {
                var zdo = (long.Parse(moveReplies[i].Id[0], CultureInfo.InvariantCulture), uint.Parse(moveReplies[i].Id[1], CultureInfo.InvariantCulture));
                if (RecordMove(call, i, zdo))
                {
                    movedBack.Add((call.MoveFor[i].Source, zdo));
                }
            }
        }
        // Objects of the world made again in the game (an applied delete undone): followed under their new
        // ZDOID from now on (WorldSave.MergeLive), as the world's own.
        for (int i = 0; i < ids.Count && i < call.CreateFor.Count; i++)
        {
            if (call.CreateFor[i] >= 0)
            {
                world.SetLiveId(call.CreateFor[i], (long.Parse(ids[i][0], CultureInfo.InvariantCulture), uint.Parse(ids[i][1], CultureInfo.InvariantCulture)));
            }
        }
        foreach (var (id, zdo) in movedBack)
        {
            if (world.ObjectRefs[id].LiveId != zdo)
            {
                world.SetLiveId(id, zdo);
            }
        }
        int destroyed = doc.RootElement.GetProperty("destroyed").GetInt32(), missing = doc.RootElement.GetProperty("missing").GetInt32();
        var msg = Said.Join(", ", [Said.Of("live.removed", destroyed), Said.Of("live.created", ids.Count), .. MovesSaid(call, [.. moveReplies.Select(m => m.InPlace)])]);
        if (doc.RootElement.TryGetProperty("refound", out var rf) && rf.GetInt32() is > 0 and int refound)
        {
            msg += Said.Of("live.refound", refound);
        }
        return Said.Join(", ",
        [
            msg,
            missing > 0 ? Said.Of("live.gone", missing) : Said.Empty,
            call.Skipped.Count > 0 ? Said.Of("live.skipped", call.Skipped.Count, call.Skipped[0]) : Said.Empty,
        ]);
    }

    /// <summary>
    /// Records what a move of the call did (under the lock): the game object, under the ZDOID that came back, stands for
    /// the id it moved to.
    /// </summary>
    /// <returns>Whether a snapshot object moved back (followed under that ZDOID as the world's own).</returns>
    private bool RecordMove(Call call, int i, (long User, uint Id) zdo)
    {
        var (source, from, to) = call.MoveFor[i];
        var target = new LiveTarget(zdo.User, zdo.Id, call.Moves[i].From.Prefab, call.Moves[i].Position);
        DropLive(from);
        if (source < 0)
        {
            // One of the editor's new objects, now standing for the copy that replaced it.
            SetLive(to, target);
            return false;
        }
        if (to == source)
        {
            _destroyed.Remove(source);
            _movedTo.Remove(source);
            _dataChanged.Remove(source);
            SetLive(source, target);
            return true;
        }
        _destroyed.Add(source);
        _movedTo[source] = to;
        // Its game object has the copy's data now (a copy the same but for its place: the object's own again).
        if (call.Moves[i].Data)
        {
            _dataChanged.Add(source);
        }
        else
        {
            _dataChanged.Remove(source);
        }
        SetLive(to, target);
        return false;
    }

    /// <summary>The moves of a call as the user is told them: those that change data as changes, the others as moves.</summary>
    /// <param name="call">The call.</param>
    /// <param name="inPlace">Whether each move was done in place, as the game answered.</param>
    private static List<Said> MovesSaid(Call call, List<bool> inPlace)
    {
        var plain = inPlace.Where((_, i) => i >= call.Moves.Count || !call.Moves[i].Data).ToList();
        int changed = inPlace.Count - plain.Count, kept = plain.Count(p => p);
        List<Said> said = [];
        if (plain.Count > 0)
        {
            said.Add(kept < plain.Count ? Said.Of("live.movedRemade", kept, plain.Count - kept) : Said.Of("live.moved", kept));
        }
        if (changed > 0)
        {
            said.Add(Said.Of("live.changed", changed));
        }
        return said;
    }
}
