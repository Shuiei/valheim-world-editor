using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// The card's look for each state of the game look: what it says, and which of its parts show.
/// </summary>
public sealed partial class GameLookBanner
{
    /// <summary>
    /// The card while the game's files are being read: the progress, its last line, and the note.
    /// </summary>
    internal void ShowReading(GameLook.Snapshot s)
    {
        _sawRunning = true;
        Title.Text = Lang.T("look.head.preparing");
        Message.Text = s.Message ?? Lang.T("look.msg.reading");
        Bar.IsVisible = LastLine.IsVisible = true;
        Bar.Value = s.Progress ?? 0;
        LastLine.Text = s.LastLine ?? "";
        Hint.Text = Lang.T("look.hint.reading");
    }

    /// <summary>
    /// The card when Valheim was not found: the folder row and how to find it. The folder box is filled from the
    /// game's own folder the first time this state is shown.
    /// </summary>
    /// <param name="s">The game look's state.</param>
    /// <param name="changed">Whether the state changed since the card was last shown.</param>
    internal void ShowMissing(GameLook.Snapshot s, bool changed)
    {
        Title.Text = Lang.T("look.head.missing");
        Message.Text = s.Message ?? Lang.T("look.msg.missing");
        PathRow.IsVisible = Steps.IsVisible = true;
        if (changed && string.IsNullOrEmpty(PathBox.Text))
        {
            PathBox.Text = s.Valheim ?? "";
        }
        Hint.Text = Lang.T("look.hint.missing");
    }

    /// <summary>
    /// The card after a failed read: the message, Try again and the log.
    /// </summary>
    /// <param name="s">The game look's state.</param>
    internal void ShowFailed(GameLook.Snapshot s)
    {
        Title.Text = Lang.T("look.head.failed");
        Message.Text = s.Message ?? "";
        Retry.IsVisible = ShowLog.IsVisible = true;
        Hint.Text = Lang.T("look.hint.failed");
    }

    /// <summary>
    /// The card when the game look is ready (its heading only; hidden unless it was shown running).
    /// </summary>
    internal void ShowReady()
    {
        Title.Text = Lang.T("look.head.ready");
        Message.Text = Lang.T("look.msg.ready");
        Hint.Text = "";
    }
}
