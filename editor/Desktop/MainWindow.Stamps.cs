using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class MainWindow
{
    /// <summary>
    /// The Landform tool's click with its Picture kind: the whole picture into the ground there, as high as the
    /// panel's Height (down when it is negative), one undo step.
    /// </summary>
    /// <param name="gx">Grid point, east.</param>
    /// <param name="gz">Grid point, north.</param>
    internal void PutPicture(float gx, float gz)
    {
        if (Session is not { } s)
        {
            return;
        }
        var panel = LandformPanel.Picture;
        var b = panel.Brush;
        float amount = panel.Height;
        if (b.StampData == null || MathF.Abs(amount) < 0.0005f)
        {
            Msg("landform.msg.pictureNothing");
            return;
        }
        bool clamped = false;
        s.EditGround(panel.Label, g =>
        {
            var (t, rect, c) = Sculpt.StampOnce(g, b, gx, gz, amount, s.MaskNow());
            clamped = c;
            return (t, rect);
        });
        Msg(clamped ? Said.Of("landform.msg.pictureLimit")
            : amount >= 0 ? Said.Of("landform.msg.pictureUp", MathF.Abs(amount)) : Said.Of("landform.msg.pictureDown", MathF.Abs(amount)));
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
