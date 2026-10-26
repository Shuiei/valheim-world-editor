using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class MainWindow
{
    /// <summary>
    /// Ctrl+X: <see cref="Copy"/>, then the objects copied leave the world (one undo step), to be pasted elsewhere.
    /// The ground stays as it is: a cut moves what was built, not the land under it.
    /// </summary>
    internal void Cut()
    {
        if (Session is not { } s || CopyToClipboard() is not { } clip)
        {
            return;
        }
        // An area with nothing in it copies its ground only: nothing to take away, no undo step.
        if (clip.Taken.Count == 0)
        {
            Msg("paste.msg.copiedGround", clip.W, clip.H);
            return;
        }
        s.Delete(clip.Taken);
        if (Tools.Mode == ToolMode.Select)
        {
            View.Select([]);
        }
        Msg(Tools.Mode == ToolMode.Select ? Said.Of("paste.msg.cutObjects", clip.Objects.Count) : Said.Of("paste.msg.cutArea", clip.Objects.Count));
        UpdateSaveBar();
    }

    /// <summary>
    /// Puts the Area selection (ground and shown objects) or the Select tool's objects on the clipboard.
    /// </summary>
    /// <returns>The copy, or null (the message says what was missing).</returns>
    private CopyData? CopyToClipboard()
    {
        if (Session is not { } s)
        {
            return null;
        }
        CopyData? clip;
        if (Tools.Mode == ToolMode.Select)
        {
            View.SelectTool.Commit();
            clip = CopyData.FromThings(s.Scene, [.. View.Selected], NameOfPrefab, View.GroundHeight);
            if (clip == null)
            {
                Msg("paste.msg.copyFirst");
                return null;
            }
            clip.Kept = ModCopies.Keep(s.Scene, clip);
        }
        else
        {
            var poly = View.Area.Polygon();
            var inside = poly == null ? [] : AreaPanel.ThingsInside(poly, chosenKindsOnly: false);
            clip = CopyData.FromArea(View.Area, s.Ground, s.Scene, inside, NameOfPrefab);
            if (clip == null)
            {
                Msg("paste.msg.areaFirst");
                return null;
            }
            clip.Kept = ModCopies.Keep(s.Scene, clip);
            // No objects and so no ground (it goes with buildings only), nor anything a mod keeps: nothing a paste would
            // put down. The clipboard keeps what it had.
            if (clip.Empty)
            {
                Msg("paste.msg.nothingToCopy");
                return null;
            }
        }
        View.Paste.Clip = clip;
        View.Paste.Notify();
        return clip;
    }

    /// <summary>
    /// Ctrl+V: the Paste tool.
    /// </summary>
    internal void StartPaste()
    {
        if (View.Paste.Clip == null)
        {
            Msg("paste.msg.copyAreaFirst");
            return;
        }
        View.SelectTool.Commit();
        Tools.ChooseMode(ToolMode.Paste);
        PastePanel.Refresh();
    }
}
