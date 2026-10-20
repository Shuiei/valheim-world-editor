using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class SelectTool
{
    /// <summary>Selects every shown object (Ctrl + A).</summary>
    public void SelectAll()
    {
        if (Scene == null)
        {
            return;
        }
        var ids = Shown().ToList();
        _view.Select(ids);
        Message?.Invoke(Said.Of("select.msg.all", ids.Count), false);
    }

    /// <summary>Clears the selection (Alt + A).</summary>
    public void SelectNone()
    {
        _view.Select([]);
        Message?.Invoke(Said.Of("select.msg.none"), false);
    }

    /// <summary>Hides the selected objects in the view (Ctrl + H): not drawn, not picked, until shown again.</summary>
    public void HideSelected()
    {
        var sel = _view.Selected;
        if (sel.Count == 0)
        {
            Message?.Invoke(Said.Of("select.msg.hideFirst"), true);
            return;
        }
        _view.Hide(sel);
        Message?.Invoke(Said.Of("select.msg.hidden", sel.Count), false);
    }

    /// <summary>Shows the objects hidden in the view again (Alt + H).</summary>
    public void ShowHidden()
    {
        int n = _view.ShowHidden();
        Message?.Invoke(Said.Of("select.msg.shown", n), false);
    }

    /// <summary>Turns the camera onto the selection (Shift + F or Numpad .).</summary>
    public void Frame()
    {
        if (!_view.FrameSelection())
        {
            Message?.Invoke(Said.Of("select.msg.frameFirst"), true);
        }
    }
}
