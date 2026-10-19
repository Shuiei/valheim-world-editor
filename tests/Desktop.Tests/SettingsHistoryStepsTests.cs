using Avalonia.Headless.XUnit;
using TerrainEditor.App;
using Xunit;
using static TerrainEditor.Desktop.Tests.SettingsDialogRun;
using static TerrainEditor.Desktop.Tests.SharedButtons;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Settings window's History section: how many steps the history keeps, empty (no limit) at first, a number that
/// is not one refused, and the one saved taken by the sessions made after. It saves to the tests' settings file, so it
/// runs alone.
/// </summary>
[Collection("DataDir")]
public class SettingsHistoryStepsTests
{
    [AvaloniaFact]
    public async Task TheStepsKeptAreChosenAndSaved()
    {
        using var t = new Temp();
        int limit = EditSession.StepLimit;
        try
        {
            var settings = new AppSettings();
            var owner = Owner();
            var shown = SettingsDialog.Show(owner, settings);
            var d = TestWindows.Shown(owner);
            var steps = Named(d, "HistorySteps");
            Assert.Equal("", steps.Text);
            Assert.Equal("No limit", steps.PlaceholderText);
            foreach (string bad in new[] { "-1", "many", "1.5" })
            {
                steps.Text = bad;
                Click(Buttons(d, "Save").Single());
                Assert.Contains(Texts(d).Split(" | "), x => x.Contains("whole number from 0 up"));
            }
            Assert.Equal(0, settings.HistorySteps);
            steps.Text = " 5000 ";
            Click(Buttons(d, "Save").Single());
            Assert.True(await shown);
            Assert.Equal(5000, AppSettings.Load().HistorySteps);
            Assert.Equal(5000, EditSession.StepLimit);
            Assert.Equal(5000, TestScenes.Flat(1).KeepSteps);
            // Shown again: the number; emptied, no limit.
            shown = SettingsDialog.Show(owner, settings);
            d = TestWindows.Shown(owner);
            steps = Named(d, "HistorySteps");
            Assert.Equal("5000", steps.Text);
            steps.Text = "";
            Click(Buttons(d, "Save").Single());
            Assert.True(await shown);
            Assert.Equal(0, AppSettings.Load().HistorySteps);
            Assert.Equal(0, EditSession.StepLimit);
        }
        finally
        {
            EditSession.StepLimit = limit;
        }
    }
}
