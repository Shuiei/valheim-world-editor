using Avalonia.Threading;

namespace TerrainEditor.Desktop;

public sealed partial class AreaPanel
{
    /// <summary>
    /// The selection changed. While a box is being dragged the outline follows the mouse at once, and the rest of
    /// <see cref="Refresh"/> (the objects inside, the lists, the volume) runs once the mouse moves waiting are
    /// handled, once for all of them; otherwise it runs now.
    /// </summary>
    private void AreaChanged()
    {
        if (!Area.Dragging)
        {
            Refresh();
            return;
        }
        _view.LassoChanged();
        if (_refreshPosted)
        {
            return;
        }
        _refreshPosted = true;
        Dispatcher.UIThread.Post(() =>
        {
            // A refresh since (the button released) has done it.
            if (_refreshPosted)
            {
                Refresh();
            }
        }, DispatcherPriority.Background);
    }

    /// <summary>A refresh is posted for when the input waiting is handled (see <see cref="AreaChanged"/>).</summary>
    private bool _refreshPosted;

    /// <summary>
    /// Cut and fill: what Flatten, Raise or Lower would move, and how much the ground inside was raised
    /// and dug since generated (again after every change of the ground, as the web editor did). Worked out only
    /// for those three actions, the ones that show it.
    /// </summary>
    public void RefreshVolume()
    {
        var g = _session()?.Ground;
        VolumeText.Text = g != null && Current is Act.Flatten or Act.Raise or Act.Lower
            ? Area.Volume(g, Ground(Current) ?? AreaTool.GroundAction.Flatten, Value(HeightBox), Value(AmountBox), _session()?.MaskNow())
            : "";
    }

    /// <summary>Marks the Box or Polygon button as on.</summary>
    private void ShowShape()
    {
        BoxButton.Classes.Set("on", Area.Box);
        PolyButton.Classes.Set("on", !(Area.Box));
        Refresh();
    }
}
