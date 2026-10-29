using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class MainWindow
{
    /// <summary>
    /// What a dug cave says: roofed with some floor open to the sky, too shallow for a roof, or dug (with what was
    /// taken away, walls that could not rise and the game's limit reached).
    /// </summary>
    /// <param name="boulders">How many boulders roof it.</param>
    /// <param name="uncovered">Floor points left open to the sky.</param>
    /// <param name="removed">Trees and rocks taken away from the trench.</param>
    /// <param name="length">The cave's length (m).</param>
    /// <param name="walls">Points where the walls could not rise to the rock.</param>
    /// <param name="clamped">Whether part of it reached the game's ±8 m limit.</param>
    /// <returns>The message, in the language in use and in English.</returns>
    internal static Said CaveOutcome(int boulders, int uncovered, int removed, float length, int walls, bool clamped)
    {
        if (boulders == 0)
        {
            return Said.Of("cave.msg.noRoof");
        }
        if (uncovered > 0)
        {
            return Said.Of("cave.msg.uncovered", boulders, uncovered);
        }
        return (removed > 0 ? Said.Of("cave.msg.dugCleared", length, boulders, removed) : Said.Of("cave.msg.dug", length, boulders))
            + (walls > 0 ? Said.Of("cave.msg.walls", walls) : Said.Empty)
            + (clamped ? Said.Of("cave.msg.limit") : Said.Of("cave.msg.undo"));
    }

    /// <summary>
    /// Stamp once (Raise or Lower with a stamp): the whole picture into the ground, one undo step.
    /// </summary>
    /// <param name="gx">Grid point, east.</param>
    /// <param name="gz">Grid point, north.</param>
    internal void StampAt(float gx, float gz)
    {
        if (Session is not { } s)
        {
            return;
        }
        var b = Tools.Brush;
        float amount = b.StampHeight * (Tools.Tool == BrushTool.Lower ? -1 : 1);
        bool clamped = false;
        s.EditGround(Lang.T("brush.shape.stamp", b.StampLabel), g =>
        {
            var (t, rect, c) = Sculpt.StampOnce(g, b, gx, gz, amount, s.MaskNow());
            clamped = c;
            return (t, rect);
        });
        Msg(clamped ? Said.Of("brush.msg.stampLimit")
            : amount >= 0 ? Said.Of("brush.msg.stampedUp", MathF.Abs(amount)) : Said.Of("brush.msg.stampedDown", MathF.Abs(amount)));
        UpdateSaveBar();
    }

    /// <summary>
    /// The Script tool's Run: the panel's script on the open area (RunScriptCode), its output below it.
    /// </summary>
    internal async Task RunScript()
    {
        if (Session == null)
        {
            Msg("script.msg.noArea");
            return;
        }
        if (_stopScript != null)
        {
            return;
        }
        var panel = ScriptPanel;
        // Pressed by Claude (set_control): the code may be Claude's as much as run_script's, so the user is asked the same.
        if (ClaudeTools.Acting.Value && !await ClaudeMayRun(panel.CodeBox.Text ?? ""))
        {
            Msg("claude.script.refused");
            return;
        }
        string name = panel.ChosenName ?? Lang.T("script.unnamed");
        panel.Running(true);
        try
        {
            var r = await RunScriptCode(panel.CodeBox.Text ?? "", Lang.T("script.undo", name), t => panel.Output.Text = t);
            panel.Output.Text = r.Output.Shown;
            Msg(r.Message);
        }
        finally
        {
            panel.Running(false);
        }
    }

    /// <summary>
    /// What a script gave.
    /// </summary>
    /// <param name="Changed">Whether its changes went in.</param>
    /// <param name="Output">What it printed and what became of it, the latter in both languages (Claude's in English).</param>
    /// <param name="Message">The status bar's line, in both languages.</param>
    internal sealed record ScriptOutcome(bool Changed, Said Output, Said Message);

    /// <summary>
    /// What a script printed, then what became of it (<paramref name="end"/>) in each language: the printed text is the
    /// same in both.
    /// </summary>
    /// <param name="printed">What the script printed (empty for none).</param>
    /// <param name="end">What became of it.</param>
    private static Said Printed(System.Text.StringBuilder printed, Said end)
    {
        string text = printed.ToString();
        return new(text + end.Shown, text + end.English);
    }
}
