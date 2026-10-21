using Avalonia.Input;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The selection keys (SelectHow), as in Blender or a map editor: a click alone takes that object, Ctrl toggles it,
/// Shift takes it out, Ctrl + Shift takes a row; a zone replaces, adds with Ctrl, takes out with Shift. Cmd is Ctrl.
/// </summary>
public sealed class SelectHowTests
{
    [Fact]
    public void AClickTakesTogglesRemovesOrTakesARow()
    {
        Assert.Equal(PickHow.Only, SelectHow.Pick(KeyModifiers.None));
        Assert.Equal(PickHow.Only, SelectHow.Pick(KeyModifiers.Alt));
        Assert.Equal(PickHow.Toggle, SelectHow.Pick(KeyModifiers.Control));
        Assert.Equal(PickHow.Toggle, SelectHow.Pick(KeyModifiers.Meta));
        Assert.Equal(PickHow.Remove, SelectHow.Pick(KeyModifiers.Shift));
        Assert.Equal(PickHow.Row, SelectHow.Pick(KeyModifiers.Control | KeyModifiers.Shift));
    }

    [Fact]
    public void AZoneReplacesAddsOrTakesOut()
    {
        Assert.Equal(ZoneHow.Replace, SelectHow.Zone(KeyModifiers.None));
        Assert.Equal(ZoneHow.Replace, SelectHow.Zone(KeyModifiers.Alt));
        Assert.Equal(ZoneHow.Add, SelectHow.Zone(KeyModifiers.Control));
        Assert.Equal(ZoneHow.Add, SelectHow.Zone(KeyModifiers.Meta));
        Assert.Equal(ZoneHow.Remove, SelectHow.Zone(KeyModifiers.Shift));
        Assert.Equal(ZoneHow.Add, SelectHow.Zone(KeyModifiers.Control | KeyModifiers.Shift));
    }
}
