using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using TerrainEditor.App;
using Xunit;
using static TerrainEditor.Desktop.Tests.SettingsDialogRun;
using static TerrainEditor.Desktop.Tests.SharedButtons;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Settings window's folders: what it shows from the settings, Browse (the folder picker), Automatic, adding and
/// taking out folders, a wrong choice explained without closing, Cancel keeping nothing, and a folder that cannot be
/// saved. It saves to the tests' settings file, so it runs alone.
/// </summary>
[Collection("DataDir")]
public class SettingsDialogTests
{
    [AvaloniaFact]
    public async Task ItShowsTheSettingsAndSavesTheChoices()
    {
        using var t = new Temp();
        string bep = t.Dir("profile"), worlds = t.Dir("worlds"), game = t.Game();
        var settings = new AppSettings { BepInExFolders = { bep }, WorldFolders = { worlds } };
        var owner = Owner();
        var shown = SettingsDialog.Show(owner, settings);
        var d = TestWindows.Shown(owner);
        Assert.Equal("Settings: folders on this computer", d.Title);
        var boxes = Boxes(d);
        // The Valheim folder (empty: automatic), then one row per folder.
        Assert.Equal([null, bep, worlds], boxes.Select(b => b.Text));
        Assert.Matches("^(Found automatically: |Not found automatically: choose the folder.)", Texts(d).Split(" | ").First(x => x.Contains("automatically")));
        boxes[0].Text = game;
        Click(Buttons(d, "Save").Single());
        Assert.True(await shown);
        Assert.Empty(owner.OwnedWindows);
        SettingsChecks.AssertKept(game, bep, worlds);
    }

    [AvaloniaFact]
    public async Task AWrongChoiceIsExplainedAndTheWindowStays()
    {
        using var t = new Temp();
        var settings = new AppSettings();
        var owner = Owner();
        var shown = SettingsDialog.Show(owner, settings);
        var d = TestWindows.Shown(owner);
        Boxes(d)[0].Text = t.Dir("NotTheGame");
        // The other choices made too: not kept either.
        d.GetLogicalDescendants().OfType<CheckBox>().Single(c => c.Content as string == "Allow Claude to connect").IsChecked = true;
        d.GetLogicalDescendants().OfType<CheckBox>().Single(c => c.Content as string == "Draw with the graphics driver's OpenGL").IsChecked = true;
        Click(Buttons(d, "Save").Single());
        Assert.Contains("The Valheim folder must be the game's folder", Texts(d));
        Assert.Single(owner.OwnedWindows);
        // Cancel: nothing kept.
        Click(Buttons(d, "Cancel").Single());
        Assert.False(await shown);
        Assert.Null(settings.ValheimPath);
        Assert.False(settings.ClaudeConnect);
        Assert.Null(settings.ClaudeToken);
        Assert.False(settings.DriverOpenGl);
    }

    [AvaloniaFact]
    public async Task BrowseAutomaticAndFolderRows()
    {
        using var t = new Temp();
        string game = t.Game(), bep = t.Dir("bep"), extra = t.Dir("extra");
        var settings = new AppSettings();
        var owner = Owner();
        var picks = new Queue<string?>([game, bep, null]);
        var asked = new List<string>();
        SettingsDialog.PickFolder = title => { asked.Add(title); return Task.FromResult(picks.Dequeue()); };
        try
        {
            var shown = SettingsDialog.Show(owner, settings);
            var d = TestWindows.Shown(owner);
            // Browse beside the Valheim folder fills it; Automatic empties it.
            Click(Buttons(d, "Browse…")[0]);
            Assert.Equal(game, Boxes(d)[0].Text);
            Click(Buttons(d, "Automatic").Single());
            Assert.Equal("", Boxes(d)[0].Text);
            // Add a BepInEx folder, browse for it; add a world folder and take it out again.
            var adds = Buttons(d, "Add a folder");
            Click(adds[0]);
            Click(Buttons(d, "Browse…")[1]);
            Assert.Equal(bep, Boxes(d)[1].Text);
            Click(adds[1]);
            // The Valheim folder and the two rows.
            Assert.Equal(3, Boxes(d).Count);
            // A cancelled picker leaves the box as it was.
            Boxes(d)[2].Text = extra;
            Click(Buttons(d, "Browse…")[2]);
            Assert.Equal(extra, Boxes(d)[2].Text);
            Click(Buttons(d, "✕")[1]);
            Assert.Equal(2, Boxes(d).Count);
            Click(Buttons(d, "Save").Single());
            Assert.True(await shown);
            Assert.Null(settings.ValheimPath);
            Assert.Equal(new[] { bep }, settings.BepInExFolders);
            Assert.Empty(settings.WorldFolders);
            Assert.Equal(3, asked.Count);
            Assert.Contains("valheim_Data", asked[0]);
        }
        finally
        {
            SettingsDialog.PickFolder = null;
        }
    }

    [AvaloniaFact]
    public void SettingsThatCannotBeSavedAreSaidAndNotKept()
    {
        using var t = new Temp();
        string? was = AppSettings.PathOverride;
        // A folder where the file should be: it cannot be written.
        AppSettings.PathOverride = t.Dir("settings.json");
        try
        {
            var settings = new AppSettings();
            string worlds = t.Dir("Worlds");
            string? problem = SettingsDialog.Apply(settings, null, [], [worlds]);
            Assert.StartsWith("The settings could not be saved: ", problem);
            Assert.Empty(settings.WorldFolders);
        }
        finally
        {
            AppSettings.PathOverride = was;
        }
    }

    [AvaloniaFact]
    public void AHomePathAndQuotesAreUnderstood()
    {
        var settings = new AppSettings();
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string? problem = SettingsDialog.Apply(settings, null, ["\"~/no-such-folder-for-vwe-tests\""], []);
        Assert.Contains(Path.Combine(home, "no-such-folder-for-vwe-tests"), problem);
    }
}
