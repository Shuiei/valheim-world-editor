using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class AreaPanel
{
    /// <summary>Runs a ground action on the selection, as one undo step, and says what it did.</summary>
    /// <param name="session">The session the change is committed to.</param>
    /// <param name="act">The action chosen (Paint names its paint).</param>
    /// <param name="ga">The ground action behind it.</param>
    private void ApplyGround(EditSession session, Act act, AreaTool.GroundAction ga)
    {
        var paintTool = Paints[Math.Max(0, PaintBox.SelectedIndex)];
        float[] paint = Brush.PaintOf(paintTool)!;
        var name = act == Act.Paint ? PaintNamed(paintTool) : AreaTool.Named(ga);
        bool clamped = false;
        var touched = session.EditGround(Lang.T("area.undo.ground", name.Shown), g =>
        {
            var (t, rect, c) = Area.Apply(g, session.Brush, ga, Value(HeightBox), Value(AmountBox), paint, session.MaskNow());
            clamped = c;
            return (t, rect);
        });
        Say(clamped ? Said.Of("area.msg.appliedLimit", name, touched.Count) : Said.Of("area.msg.applied", name, touched.Count));
    }
}
