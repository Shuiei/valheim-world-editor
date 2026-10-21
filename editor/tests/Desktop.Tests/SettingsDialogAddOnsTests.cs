using TerrainEditor.App;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// What Settings says of an installed add-on (SettingsDialog's add-ons list): turned off, waiting for the mod it serves, in use with
/// only its notes, or its extension's state (not read yet when no extension of it was read). Runs alone (the
/// extensions are shared).
/// </summary>
[Collection("DataDir")]
public sealed class SettingsDialogAddOnsTests : IDisposable
{
    /// <summary>Puts no extension back in use.</summary>
    public void Dispose() => ModExtensions.Forget();

    [Fact]
    public void AnAddOnsStateIsSaid()
    {
        ModExtensions.Forget();
        Assert.Equal(Lang.T("settings.addOnOff"), SettingsDialog.Form.AddOnState(new AddOns.AddOn("Digging", "/addons/Digging", "DigMod.dll", true, "/addons/Digging/Dig.dll", On: false)));
        Assert.Equal(Lang.T("settings.addOnWaiting", "DigMod.dll"), SettingsDialog.Form.AddOnState(new AddOns.AddOn("Digging", "/addons/Digging", "DigMod.dll", false, "/addons/Digging/Dig.dll")));
        Assert.Equal(Lang.T("settings.addOnNotes"), SettingsDialog.Form.AddOnState(new AddOns.AddOn("Notes", "/addons/Notes", null, true, null)));
        Assert.Equal(Lang.T("settings.addOnNotRead"), SettingsDialog.Form.AddOnState(new AddOns.AddOn("Digging", "/addons/Digging", "DigMod.dll", true, "/addons/Digging/Dig.dll")));
    }
}
