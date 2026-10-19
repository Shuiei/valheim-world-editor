using System.Numerics;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class SelectTool
{
    /// <summary>
    /// Ends a move: the originals are replaced by copies at their new places, and those get selected. A move that
    /// changed nothing is just dropped: one made with the mouse or keys by less than a centimetre across, a millimetre
    /// up and a thousandth of a degree round. A typed one (exact place) is always made, however small: PlaceAt never
    /// starts one that asks for nothing.
    /// </summary>
    public void Commit()
    {
        _commitTimer.Stop();
        var m = _move;
        _move = null;
        if (m == null)
        {
            return;
        }
        _view.SetPreviews(null);
        if (!m.NoSnap && MathF.Abs(m.Dx) + MathF.Abs(m.Dz) < 0.01f && MathF.Abs(m.Turn) < NoTurn && MathF.Abs(m.Dy) < NoLift && !m.Dropped || Session is not { } session)
        {
            return;
        }
        GroundBlock(m);
        Snap(m);
        var moves = m.Items.Select(it => (it.Index, Target(m, it), it.Thing.Rotation + new Vector3(0, m.Turn, 0), it.Thing.Scale)).ToList();
        // A creature moved into a piece stands on top of it.
        int stood = CreatureStand.Apply(session.Scene, null, moves);
        // The step is whoever started the move's: a key move of the user's committed by a control Claude sets (the
        // Select panel's Apply) stays the user's, and one Claude started stays Claude's when the timer commits it.
        bool acting = ClaudeTools.Acting.Value;
        ClaudeTools.Acting.Value = m.ByClaude;
        List<int> copies;
        try
        {
            copies = session.Transform(moves, Lang.T("history.undo.moved", moves.Count));
        }
        finally
        {
            ClaudeTools.Acting.Value = acting;
        }
        _view.Select(copies);
        Message?.Invoke(Said.Of("select.msg.moved", copies.Count, WholeGroups(m.Groups, m.Noun, deleted: false)) + CreatureStand.Note(stood), false);
    }

    /// <summary>What a message adds when whole linked groups go along (empty when none): one clause per case.</summary>
    /// <param name="count">How many groups.</param>
    /// <param name="noun">What a group is called ("vehicle").</param>
    /// <param name="deleted">They were deleted (their parts never go alone) rather than moved.</param>
    private static Said WholeGroups(int count, string noun, bool deleted) => count switch
    {
        0 => Said.Empty,
        1 => Said.Of(deleted ? "select.msg.wholeGroup.go" : "select.msg.wholeGroup.move", Noun(noun)),
        _ => Said.Of(deleted ? "select.msg.wholeGroups.go" : "select.msg.wholeGroups.move", Noun(noun), count),
    };
}
