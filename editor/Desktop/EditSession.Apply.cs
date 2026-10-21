
namespace TerrainEditor.Desktop;

/// <summary>
/// Live: applying the changes to the running game.
/// </summary>
public sealed partial class EditSession
{
    /// <summary>
    /// Live: applies everything to the running game. After zone resets the world is read again from the game (like a
    /// save), and after No limit ground became ground discs the area is (the ground under it changed); otherwise the
    /// history stays.
    /// </summary>
    /// <param name="confirm">See WorldSession.ApplyLive (asked before writing over ground the game changed).</param>
    public async Task<WorldSession.Outcome> ApplyLive(Func<IReadOnlyList<(int X, int Z)>, Task<bool>>? confirm = null)
    {
        var owner = Scene.Owner!;
        // The steps there are now: a step made while the game answers may not be in what was sent.
        List<Change> before;
        lock (_lock)
        {
            before = [.. _undo];
        }
        var o = await owner.ApplyLive(confirm);
        lock (_lock)
        {
            if (o.Reloaded || o.Lifted)
            {
                // A stroke begun while the game answered was made on the ground as it was before: it is dropped (the
                // area read again has none of it, and its undo would write the old ground back).
                if (_stroke != null)
                {
                    _stroke = null;
                    _strokeMask = null;
                    _strokeDropped = true;
                }
                _takeEditsAfterStroke = false;
                Reread(owner.World);
            }
            else
            {
                // During a stroke, the ground taken from the edits now would lose what the stroke did so far.
                if (_stroke != null)
                {
                    _takeEditsAfterStroke = true;
                }
                else
                {
                    Ground.TakeEdits(Edits);
                }
                if (o.Done)
                {
                    MarkApplied(before, o.Unsent == WorldSession.LiveParts.None ? null : o.Sent);
                }
            }
        }
        if (o.Reloaded || o.Lifted)
        {
            ThingsReset?.Invoke();
        }
        Changed?.Invoke();
        return o;
    }
}
