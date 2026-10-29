using Avalonia.Controls;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

// The exact place: Move there or by the boxes' values, and the boxes' Move by mode.
public sealed partial class SelectPanel
{
    /// <summary>
    /// Moves the selection to (or by) the boxes' values, then back to showing where it is. Move there moves only along
    /// the axes whose box was changed, from the unrounded place the boxes were filled from: a box left as Refresh put
    /// it (rounded to a centimetre or a tenth of a degree) is not a move.
    /// </summary>
    private void Apply()
    {
        static float V(NumericUpDown b) => (float)(b.Value ?? 0);
        if (_by)
        {
            // An empty or zero box is no move along that axis.
            static float? By(NumericUpDown b) => b.Value is { } v && v != 0 ? (float)v : null;
            _tool.PlaceAt(By(XBox), By(YBox), By(ZBox), By(TurnBox), by: true);
            SetBy(false);
            return;
        }
        if (_shown is not { } was)
        {
            return;
        }
        // Which boxes were changed, and to what, taken before a move in progress (a turn or lift from the keys, not
        // committed yet) is committed: the boxes were filled before it, and the place to move from is after it.
        static bool Changed(NumericUpDown b, float from, int digits) => b.Value != (decimal)MathF.Round(from, digits);
        var (x, y, z, turn) = (V(XBox), V(YBox), V(ZBox), V(TurnBox));
        var (cx, cy, cz, ct) = (Changed(XBox, was.X, 2), Changed(YBox, was.Y, 2), Changed(ZBox, was.Z, 2), Changed(TurnBox, was.Turn, 1));
        if (_tool.Moving)
        {
            _tool.Commit();
            Refresh();
        }
        if (_shown is { } at)
        {
            _tool.PlaceAt(cx ? x - at.X : null, cy ? y - at.Y : null, cz ? z - at.Z : null, ct ? turn - at.Turn : null, by: true);
        }
        SetBy(false);
    }

    /// <summary>Switches Move by: on, the boxes start at 0; off, they show where the selection is.</summary>
    private void SetBy(bool by)
    {
        _by = by;
        ApplyButton.Content = by ? Lang.T("select.label.moveByThese") : Lang.T("select.label.moveThere");
        ByButton.Classes.Set("on", by);
        if (by)
        {
            XBox.Value = YBox.Value = ZBox.Value = TurnBox.Value = 0;
            _info.Text = Lang.T("select.hint.by");
        }
        else
        {
            Refresh();
        }
    }
}
