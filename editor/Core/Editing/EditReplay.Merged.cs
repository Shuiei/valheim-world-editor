
namespace TerrainEditor.Editing;

public sealed partial class EditReplay
{
    /// <summary>
    /// A zone both changed, the editor's kept: the editor's height and paint where it changed them, the game's
    /// elsewhere; No limit ground is the editor's alone. A point is the editor's change where it differs from what the
    /// game's save started from there: the editor's save, where the game still has what that save wrote (an undo since
    /// that save counts), else the save read.
    /// </summary>
    /// <param name="mine">The editor's zone.</param>
    /// <param name="read">The zone as the editor read it.</param>
    /// <param name="last">The zone as the editor's save wrote it (as read, when it wrote none).</param>
    /// <param name="game">The zone as the game saved it.</param>
    private static ZoneEdit Merged(ZoneEdit mine, ZoneEdit read, ZoneEdit last, ZoneEdit game)
    {
        var m = game.Clone();
        for (int i = 0; i < EditStore.Cells; i++)
        {
            var from = SameHeight(game, last, i) ? last : read;
            if (!SameHeight(mine, from, i))
            {
                m.Modified[i] = mine.Modified[i];
                m.Level[i] = mine.Level[i];
                m.Smooth[i] = mine.Smooth[i];
            }
            if (!SamePaint(mine, SamePaint(game, last, i) ? last : read, i))
            {
                m.PaintModified[i] = mine.PaintModified[i];
                Array.Copy(mine.Paint, i * 4, m.Paint, i * 4, 4);
            }
            m.Lift[i] = mine.Lift[i];
        }
        return m;
    }

    /// <summary>The largest difference two values of a point can have and be the same (as ZoneEdit.SameGround allows).</summary>
    private const float Same = 1e-4f;

    /// <summary>Whether two zones have a point's height change alike (whether it is changed, its level and smoothing).</summary>
    private static bool SameHeight(ZoneEdit a, ZoneEdit b, int i) =>
        a.Modified[i] == b.Modified[i] && (!a.Modified[i] || MathF.Abs(a.Level[i] - b.Level[i]) <= Same && MathF.Abs(a.Smooth[i] - b.Smooth[i]) <= Same);

    /// <summary>Whether two zones have a point's paint alike (whether it is painted, and its four values).</summary>
    private static bool SamePaint(ZoneEdit a, ZoneEdit b, int i)
    {
        if (a.PaintModified[i] != b.PaintModified[i])
        {
            return false;
        }
        for (int c = 0; c < 4 && a.PaintModified[i]; c++)
        {
            if (MathF.Abs(a.Paint[i * 4 + c] - b.Paint[i * 4 + c]) > Same)
            {
                return false;
            }
        }
        return true;
    }
}
