using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;
using static TerrainEditor.Desktop.Tests.InspectorControls;
using static TerrainEditor.Desktop.Tests.SharedButtons;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The picked slot's item box: a whole item name typed puts it in the slot, and the box keeps the keyboard.
/// </summary>
[Collection("World files")]
public class InspectorPanelSlotChoiceTests
{
    /// <summary>The picked slot's item box as drawn now.</summary>
    private static AutoCompleteBox Pick(InspectorPanel i) =>
        i.ItemsBox.GetLogicalDescendants().OfType<AutoCompleteBox>().First(x => x.PlaceholderText == "Find an item…");

    /// <summary>Capital letters typed as the keyboard sends them (Shift held): key down, the text, key up.</summary>
    private static void Type(Window w, string text)
    {
        foreach (char c in text)
        {
            var key = Enum.Parse<Key>(char.ToUpperInvariant(c).ToString());
            var physical = Enum.Parse<PhysicalKey>(char.ToUpperInvariant(c).ToString());
            var mods = char.IsUpper(c) ? RawInputModifiers.Shift : RawInputModifiers.None;
            w.KeyPress(key, mods, physical, c.ToString());
            w.KeyTextInput(c.ToString());
            w.KeyRelease(key, mods, physical, c.ToString());
            Dispatcher.UIThread.RunJobs();
        }
    }

    [AvaloniaFact]
    public void TypingOnPastAWholeItemNameStaysInTheBox()
    {
        using var r = new SharedInspectorRun();
        Assert.True(r.I.Open(r.ChestIndex));
        r.I.Items!.Clear();
        Click(Slot(r.I, 0, 0));
        var mode = r.W.Tools.Mode;
        Pick(r.I).Focus();
        Dispatcher.UIThread.RunJobs();
        // "Iron" is an item: it goes in the slot, and the grid is drawn again with a new box.
        Type(r.W, "Iron");
        Assert.Equal("Iron", Assert.Single(r.I.Items).Name);
        // The new box has the keyboard, the caret after its text: the next keys are its own, not the window's
        // shortcuts (S, C and A moved the camera, P chose Path).
        var focused = r.W.FocusManager!.GetFocusedElement() as Control;
        Assert.Same(Pick(r.I), focused?.FindAncestorOfType<AutoCompleteBox>(includeSelf: true));
        Type(r.W, "Scrap");
        Assert.Equal("IronScrap", Assert.Single(r.I.Items).Name);
        Assert.Equal(mode, r.W.Tools.Mode);
        Assert.Empty(r.W.View.KeysHeld);
    }
}
