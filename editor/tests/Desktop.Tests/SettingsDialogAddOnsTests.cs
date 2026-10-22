using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using TerrainEditor.App;
using TerrainEditor.Save;
using Xunit;
using static TerrainEditor.Desktop.Tests.SettingsDialogRun;
using static TerrainEditor.Desktop.Tests.SharedButtons;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// What Settings says of an installed add-on (SettingsDialog's add-ons list): turned off, waiting for the mod it serves, in use with
/// only its notes, or its extension's state (not read yet when no extension of it was read); and its switch, saved,
/// used at once. Runs alone (the manifests and extensions are shared).
/// </summary>
[Collection("DataDir")]
public sealed class SettingsDialogAddOnsTests : IDisposable
{
    private readonly bool _defaults = LocalGame.SearchDefaultPlaces;

    public SettingsDialogAddOnsTests() => LocalGame.SearchDefaultPlaces = false;

    /// <summary>Puts no manifest or extension back in use, the program's folder and the default places back.</summary>
    public void Dispose()
    {
        ModExtensions.Forget();
        ModManifests.Forget();
        AddOns.FolderForTests = null;
        LocalGame.SearchDefaultPlaces = _defaults;
    }

    [Fact]
    public void AnAddOnsStateIsSaid()
    {
        ModExtensions.Forget();
        Assert.Equal(Lang.T("settings.addOnOff"), SettingsDialog.Form.AddOnState(new AddOns.AddOn("Digging", "/addons/Digging", "DigMod.dll", true, "/addons/Digging/Dig.dll", On: false)));
        Assert.Equal(Lang.T("settings.addOnWaiting", "DigMod.dll"), SettingsDialog.Form.AddOnState(new AddOns.AddOn("Digging", "/addons/Digging", "DigMod.dll", false, "/addons/Digging/Dig.dll")));
        Assert.Equal(Lang.T("settings.addOnNotes"), SettingsDialog.Form.AddOnState(new AddOns.AddOn("Notes", "/addons/Notes", null, true, null)));
        Assert.Equal(Lang.T("settings.addOnNotRead"), SettingsDialog.Form.AddOnState(new AddOns.AddOn("Digging", "/addons/Digging", "DigMod.dll", true, "/addons/Digging/Dig.dll")));
    }

    [AvaloniaFact]
    public async Task AnAddOnTurnedOnIsUsedAsSoonAsTheSettingsAreSaved()
    {
        using var t = new Temp();
        // An add-on of notes only (no mod to serve: used whenever it is on), turned off.
        AddOns.FolderForTests = t.Dir("program");
        string notes = t.Dir(Path.Combine("program", AddOns.FolderName, "Notes"));
        File.WriteAllText(Path.Combine(notes, ModManifests.FileName), """{ "version": 1, "prefabs": { "notes_holder": { "data": true } } }""");
        var settings = new AppSettings { AddOnsOff = ["Notes"] };
        ModManifests.Refresh(settings);
        Assert.False(ModManifests.IsData(StableHash.Of("notes_holder")));

        var owner = Owner();
        var shown = SettingsDialog.Show(owner, settings);
        var d = TestWindows.Shown(owner);
        d.GetLogicalDescendants().OfType<CheckBox>().Single(c => c.Content as string == "Notes").IsChecked = true;
        Click(Buttons(d, "Save").Single());
        Assert.True(await shown);
        Assert.Empty(settings.AddOnsOff);
        Assert.True(ModManifests.IsData(StableHash.Of("notes_holder")));
    }
}
