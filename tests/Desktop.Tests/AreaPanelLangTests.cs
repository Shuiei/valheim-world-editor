using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using TerrainEditor.App;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Area tool in another language (the made-up one: PseudoLang): its panel with no selection (box and polygon),
/// with a selection under every action, with a heightmap read, and its Reset zones question show only catalogue
/// texts; every action's message (ground, remove, regrow, replace, reset and its cancelling, backups, heightmaps) and
/// History step is shown in the language with its English kept for Claude; the cut and fill readout and the clipboard
/// line are translated with their units; Claude's own ground changes are named in English.
/// </summary>
[Collection("DataDir")]
public sealed class AreaPanelLangTests
{
    /// <summary>Clicks the button and runs the posted work it starts.</summary>
    private static void Click(Button b)
    {
        b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public async Task AreaPanelShowsOnlyCatalogueTexts()
    {
        using var pseudo = new PseudoLang();
        using var r = new AreaPanelTests.Run(select: false);
        string maps = Path.Combine(Path.GetDirectoryName(r.Dir)!, "maps");
        var p = r.P;
        void Check()
        {
            Dispatcher.UIThread.RunJobs();
            // The prefab names of the Replace lists are data, shown as they are.
            string[] data = [.. new[] { p.FromBox, p.ToBox }.SelectMany(b => b.Items.OfType<string>())];
            Assert.Empty(PseudoLang.Untranslated(p.Card, data));
        }

        // No selection: how to make one, as a box and as a polygon.
        Check();
        Click(p.PolyButton);
        Check();
        Click(p.BoxButton);
        r.Select(2, 2, 62, 62);
        foreach (var act in Enum.GetValues<AreaPanel.Act>())
        {
            p.Choose(act);
            Check();
        }

        // A heightmap read: what it is fitted to.
        p.Choose(AreaPanel.Act.Heightmap);
        var written = p.ExportHeightmap(maps)!;
        p.LoadPicture(Directory.GetFiles(maps).Single());
        Check();
        Assert.StartsWith(PseudoLang.Open.ToString(), written.Shown);

        // A message: shown in the language, its English kept for Claude.
        Click(p.ClearButton);
        Assert.Equal(PseudoLang.Wrap("Selection cleared."), r.W.MessageText.Text);
        Assert.Equal("Selection cleared.", r.W.LastMessageEn);

        // The Reset zones question.
        r.Select(2, 2, 62, 62);
        p.Choose(AreaPanel.Act.Reset);
        var applied = p.Apply();
        var d = TestWindows.Shown(r.W);
        Assert.Empty(PseudoLang.Untranslated(d));
        DialogsTests.Press(d, Lang.T("common.cancel"));
        await applied;
        Assert.Empty(r.S.Resets);
    }

    [AvaloniaFact]
    public async Task ClaudesGroundChangesAreNamedInEnglish()
    {
        using var r = new ClaudeRun();
        await r.OpenArea();
        using var pseudo = new PseudoLang();
        string said = await r.T.ShapeGround("restore", -10, -10, 10, 10, edge: 0);
        Assert.StartsWith("Restore the ground: ", said);
        Assert.DoesNotContain(PseudoLang.Open, said[..said.IndexOf(" Now pending", StringComparison.Ordinal)]);
        Assert.Equal("Claude: restore the ground", r.W.Session!.UndoLabel);
    }

    /// <summary>
    /// Every message the panel gave is shown in the language with its English kept, and among them are messages
    /// starting with each of these English beginnings.
    /// </summary>
    private static void ShownAndReadInEnglish(List<Said> told, params string[] starts)
    {
        Assert.All(told, t =>
        {
            Assert.StartsWith(PseudoLang.Open.ToString(), t.Shown);
            Assert.DoesNotContain(PseudoLang.Open, t.English);
        });
        Assert.All(starts, start => Assert.Contains(told, t => t.English.StartsWith(start, StringComparison.Ordinal)));
    }

    /// <summary>The History step's name is in the language.</summary>
    private static void StepShown(AreaPanelTests.Run r) => Assert.StartsWith(PseudoLang.Open.ToString(), r.S.UndoLabel);

    [AvaloniaFact]
    public async Task GroundAndObjectActionsSayWhatTheyDidInTheLanguage()
    {
        using var pseudo = new PseudoLang();
        using var r = new AreaPanelTests.Run();
        var told = new List<Said>();
        r.P.Message += told.Add;
        // Raise: its cut and fill readout, units inside each translated line, then applied.
        r.P.AmountBox.Value = 1.5m;
        r.P.Choose(AreaPanel.Act.Raise);
        string[] lines = r.P.VolumeText.Text!.Split('\n');
        Assert.Equal(2, lines.Length);
        Assert.Matches(@"^⟦Raise 1\.5 m: fill ⟦[\d.]+k? m³⟧\.⟧$", lines[0]);
        Assert.Matches(@"^⟦Ground inside: ⟦[\d.]+k? m³⟧ raised, ⟦[\d.]+k? m³⟧ dug since generated\.⟧$", lines[1]);
        await r.P.Apply();
        StepShown(r);
        // Remove with none of the kinds chosen, then trees removed and regrown.
        r.ChooseNoKinds();
        r.P.Choose(AreaPanel.Act.Remove);
        await r.P.Apply();
        r.P.KindButtons[ObjectKind.Trees].IsChecked = true;
        await r.P.Apply();
        StepShown(r);
        r.P.Choose(AreaPanel.Act.Regrow);
        await r.P.Apply();
        StepShown(r);
        // Replace without its lists chosen, then with them.
        await r.ReplaceWithNothingChosen();
        var from = r.P.FromBox.Items.OfType<string>().ToList();
        r.P.FromBox.SelectedIndex = 0;
        r.P.ToBox.SelectedIndex = r.P.ToBox.Items.OfType<string>().ToList().FindIndex(t => t != from[0].Split(" (")[0]);
        await r.P.Apply();
        StepShown(r);
        ShownAndReadInEnglish(told, "Raise applied to", "No objects of the chosen kinds", "Removed ", "Regrew ",
            "Choose what to replace, and with what.", "Replaced ");
    }

    [AvaloniaFact]
    public async Task ResetsBackupsAndHeightmapsSayWhatTheyDidInTheLanguage()
    {
        using var pseudo = new PseudoLang();
        using var r = new AreaPanelTests.Run();
        var told = new List<Said>();
        r.P.Message += told.Add;
        // Zones marked for reset and the mark cancelled, then nothing left to cancel.
        r.P.Confirm = _ => Task.FromResult(true);
        r.P.Choose(AreaPanel.Act.Reset);
        await r.P.Apply();
        StepShown(r);
        Click(r.P.UnresetButton);
        StepShown(r);
        Click(r.P.UnresetButton);
        // Backups: none chosen, another world and this one read, nothing to restore.
        r.P.Choose(AreaPanel.Act.Backup);
        await r.P.Apply();
        var other = TerrainEditor.Save.WorldSave.Load(r.Dir);
        typeof(TerrainEditor.Save.WorldSave).GetProperty(nameof(TerrainEditor.Save.WorldSave.Seed))!.SetValue(other, other.Seed + 1);
        Assert.Null(await r.P.OpenBackup(r.Dir, other));
        Assert.StartsWith(PseudoLang.Wrap("That is another world (seed ")[..^1], r.P.BackupInfo.Text);
        Assert.NotNull(await r.P.OpenBackup(r.Dir, r.S.Scene.World));
        Assert.StartsWith(PseudoLang.Wrap("Backup: save #")[..^1], r.P.BackupInfo.Text);
        r.P.BackupGroundBox.IsChecked = false;
        r.P.BackupObjectsBox.IsChecked = false;
        await r.P.RestoreBackup(r.S, r.W.View.Area.Polygon()!, r.Dir);
        // Heightmaps: written, a picture that is not one, the written one put into the ground.
        r.P.Choose(AreaPanel.Act.Heightmap);
        string png = r.ExportHeightmap();
        r.PickABadPicture(png);
        Click(r.P.ImportButton);
        await TestWindows.Until(() => told.Any(t => t.English.StartsWith("That picture cannot be used", StringComparison.Ordinal)));
        r.P.PickPicture = () => Task.FromResult<string?>(png);
        Click(r.P.ImportButton);
        await TestWindows.Until(() => r.P.PictureInfo.Text?.StartsWith(PseudoLang.Open) == true);
        Click(r.P.PutButton);
        StepShown(r);
        ShownAndReadInEnglish(told, "1 zone(s) marked for reset", "Reset cancelled for 1 zone(s).", "No zone marked for reset",
            "Choose a backup first.", "Nothing to restore", "Heightmap written to", "That picture cannot be used", "Imported");
    }

    [AvaloniaFact]
    public void TheClipboardLineIsShownInTheLanguage()
    {
        using var pseudo = new PseudoLang();
        using var r = new AreaPanelTests.Run();
        r.P.Choose(AreaPanel.Act.Copy);
        Assert.Equal(PseudoLang.Wrap("Copies the shown objects inside the selection, with the ground under their buildings."), r.P.ClipInfo.Text);
        Click(r.P.CopyButton);
        Assert.Matches(@"^⟦Clipboard: \d+ × \d+ m, \d+ object\(s\)\.⟧$", r.P.ClipInfo.Text);
    }
}
