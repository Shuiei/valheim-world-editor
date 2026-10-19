using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class MainWindow
{
    /// <summary>
    /// Worlds: back to the start page (asks first when something is not saved).
    /// </summary>
    internal async Task LeaveWorld()
    {
        if (World is { } w && w.Pending is not (0, 0, 0, 0)
            && !await Ask(Lang.T("save.ask.leave.title"), w.IsLive ? Lang.T("save.ask.leaveLive.text", Describe(w.Pending)) : Lang.T("save.ask.leave.text", Describe(w.Pending)), Lang.T("common.leaveAnyway"), Lang.T("common.stay")))
        {
            return;
        }
        CloseWorld();
        ShowStart();
    }

    /// <summary>
    /// Closes the world open (for the start page, or the Workshop): its live connection ends with it, the map stops
    /// asking the game for its players, and nothing of it is kept for the next world.
    /// </summary>
    private void CloseWorld()
    {
        // An area still loading in it is not opened, nor the one left open again.
        _navigation++;
        // The map lets go of the world before its connection ends: no question is asked of it any more, and the
        // answer to one still waiting is dropped.
        MapPage?.Forget();
        World?.Dispose();
        World = null;
        Session = null;
        // A backup read for the Area tool's restore is not kept for the next world.
        AreaPanel.ForgetBackup();
        Tunnel.Close();
    }

    /// <summary>
    /// Says something in the status bar, in the user's language with its English kept for Claude (<c>Msg</c>), and,
    /// away from the 3D editor, in a dialog too, not waited for.
    /// </summary>
    /// <param name="said">What to say, in both languages.</param>
    private void Say(Said said)
    {
        Msg(said);
        if (_pages.Content != _editorPage)
        {
            _ = Tell(said.Shown);
        }
    }

    /// <summary>
    /// Puts a message in the status bar: the text of <paramref name="key"/> in the user's language, its English kept for
    /// Claude (<c>LastSaid</c>).
    /// </summary>
    /// <param name="key">The catalogue key, written in full.</param>
    /// <param name="values">The values for its places.</param>
    internal void Msg(string key, params object?[] values) => Msg(Said.Of(key, values));

    /// <summary>Puts a message in the status bar, shown in the user's language, its English kept for Claude.</summary>
    /// <param name="said">The message in both languages.</param>
    internal void Msg(Said said)
    {
        KeptSaid = said;
        MessageText.Text = said.Shown;
    }

    /// <summary>The last message put through <c>Msg</c>, in both languages.</summary>
    private Said KeptSaid { get; set; } = Said.Empty;

    /// <summary>
    /// The status bar's message in both languages: the one <c>Msg</c> put there, or, when the text was set directly
    /// (a text that is the same in every language, or not converted yet), that text on both sides.
    /// </summary>
    internal Said LastSaid => MessageText.Text is not { } shown ? Said.Empty : shown == KeptSaid.Shown ? KeptSaid : Said.Raw(shown);

    /// <summary>The status bar's message in English: what Claude reads back, whatever language the window shows.</summary>
    internal string LastMessageEn => LastSaid.English;

    /// <summary>
    /// An error no part of the window caught (App): said in the status bar, so the editor stays open with the changes
    /// not saved yet.
    /// </summary>
    internal void ShowError(Exception ex)
    {
        Busy(null);
        var said = Said.Of("save.msg.wentWrong", ex.Message);
        Msg(said);
        // Away from the 3D editor (its status bar not shown): once in a dialog, not one per error.
        if (_pages.Content != _editorPage && !_errorShown)
        {
            _errorShown = true;
            _ = Tell(said.Shown).ContinueWith(_ => _errorShown = false, TaskScheduler.FromCurrentSynchronizationContext());
        }
    }

    /// <summary>
    /// A dialog about an error is open: more errors go only to the status bar.
    /// </summary>
    private bool _errorShown;

    /// <summary>
    /// The Inspector panel: one object's data.
    /// </summary>
    internal InspectorPanel Inspector { get; }

    /// <summary>
    /// The Blueprints panel.
    /// </summary>
    internal BlueprintsPanel Blueprints { get; }

    /// <summary>
    /// The View panel's card.
    /// </summary>
    internal Control ViewPanelCard { get; } = null!;

    /// <summary>
    /// What is waiting to be saved, in the save bar.
    /// </summary>
    internal TextBlock PendingText { get; } = new() { FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Text = Lang.T("save.pending.allSaved") };

    /// <summary>
    /// The status bar's last message.
    /// </summary>
    internal TextBlock MessageText { get; } = new() { FontSize = 12.5, TextWrapping = TextWrapping.Wrap, MaxLines = 2, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };

    /// <summary>
    /// Asks before writing into the world (replaced by tests).
    /// </summary>
    internal Func<string, Task<bool>> ConfirmSave { get; set; }

    /// <summary>
    /// The question asked before saving into the world files, with what to keep in mind.
    /// </summary>
    /// <param name="what">What would be written, in words.</param>
    /// <param name="directory">The world's folder.</param>
    private static string SaveConfirmText(string what, string directory) => Lang.T("save.ask.save.text", what, Ui.Tilde(directory));

    /// <summary>
    /// A yes / no question (title, text, yes, no): Discard, Leave, Quit with unsaved changes. Tests answer it.
    /// </summary>
    internal Func<string, string, string, string, Task<bool>> Ask { get; set; }

    /// <summary>
    /// Tells the user something in a dialog titled with the app's name, as the map's are (tests replace it).
    /// </summary>
    internal Func<string, Task> Tell { get; set; }

    /// <summary>
    /// The area being edited, or null when none is open.
    /// </summary>
    internal EditSession? Session { get; private set; }

    /// <summary>
    /// Close without asking about what is not saved.
    /// </summary>
    private bool _closeAnyway;
}
