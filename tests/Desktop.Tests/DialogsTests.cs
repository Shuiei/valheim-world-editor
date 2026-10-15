using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The small modal windows (Dialogs): a question answered with either button, a message with only OK, and a line of
/// text typed, kept or cancelled, a blueprint's details likewise. They are real windows owned by the main one: the tests find them there and press
/// their buttons.
/// </summary>
public class DialogsTests
{

    /// <summary>
    /// Presses the dialog's button with this label.
    /// </summary>
    internal static void Press(Window dialog, string label)
    {
        var b = dialog.GetLogicalDescendants().OfType<Button>().Single(b => b.Content as string == label);
        b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>
    /// A shown 800 × 600 window to own the dialogs.
    /// </summary>
    private static Window Owner()
    {
        var w = new Window { Width = 800, Height = 600 };
        w.Show();
        return w;
    }

    [AvaloniaFact]
    public async Task AQuestionIsYesWithTheFirstButtonAndNoWithTheOther()
    {
        var owner = Owner();
        var asked = Dialogs.Ask(owner, "Discard", "Discard the changes?", "Discard", "Keep them");
        var d = TestWindows.Shown(owner);
        Assert.Equal("Discard", d.Title);
        Assert.Contains("Discard the changes?", d.GetLogicalDescendants().OfType<SelectableTextBlock>().Single().Text);
        Press(d, "Discard");
        Assert.True(await asked);
        Assert.Empty(owner.OwnedWindows);

        asked = Dialogs.Ask(owner, "Discard", "Again?", "Discard", "Keep them");
        Press(TestWindows.Shown(owner), "Keep them");
        Assert.False(await asked);
    }

    [AvaloniaFact]
    public async Task AMessageHasOnlyOk()
    {
        var owner = Owner();
        var told = Dialogs.Tell(owner, "Save", "Saved 3 zone(s).");
        var d = TestWindows.Shown(owner);
        var buttons = d.GetLogicalDescendants().OfType<Button>().ToList();
        Assert.Equal("OK", Assert.Single(buttons).Content);
        Assert.True(buttons[0].IsDefault);
        Press(d, "OK");
        await told;
        Assert.Empty(owner.OwnedWindows);
    }

    [AvaloniaFact]
    public async Task ClosingAQuestionWithoutAButtonIsNo()
    {
        var owner = Owner();
        var asked = Dialogs.Ask(owner, "Q", "Sure?", "Yes");
        TestWindows.Shown(owner).Close();
        Dispatcher.UIThread.RunJobs();
        Assert.False(await asked);
    }

    [AvaloniaFact]
    public async Task ALineOfTextIsTypedKeptOrCancelled()
    {
        var owner = Owner();
        var typed = Dialogs.AskText(owner, "Save blueprint", "Name of the blueprint:", "My house");
        var d = TestWindows.Shown(owner);
        var box = d.GetLogicalDescendants().OfType<TextBox>().Single();
        Assert.Equal("My house", box.Text);
        box.Text = "Longhouse";
        Press(d, "OK");
        Assert.Equal("Longhouse", await typed);

        typed = Dialogs.AskText(owner, "Save blueprint", "Name:");
        d = TestWindows.Shown(owner);
        Assert.Equal("", d.GetLogicalDescendants().OfType<TextBox>().Single().Text);
        Press(d, "Cancel");
        Assert.Null(await typed);
    }

    [AvaloniaFact]
    public async Task ABlueprintsDetailsAreTypedKeptOrCancelled()
    {
        var owner = Owner();
        var asked = Dialogs.AskBlueprint(owner, new("Hut", "A small hut", ["house", "wood"]), "20 Wood");
        var d = TestWindows.Shown(owner);
        Assert.Equal("Blueprint", d.Title);
        var boxes = d.GetLogicalDescendants().OfType<TextBox>().ToList();
        Assert.Equal(["Hut", "A small hut", "house, wood"], boxes.Select(b => b.Text));
        Assert.Contains(d.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text == "Cost in game: 20 Wood");
        // Not a dungeon; switched on, the tag goes with the others.
        var dungeon = Assert.Single(d.GetLogicalDescendants().OfType<CheckBox>());
        Assert.False(dungeon.IsChecked);
        dungeon.IsChecked = true;
        // The tags are split on commas and semicolons, each once.
        boxes[0].Text = "Longhouse";
        boxes[1].Text = "Two floors";
        boxes[2].Text = "stone, , Viking;stone";
        Press(d, "Save");
        var kept = await asked;
        Assert.NotNull(kept);
        Assert.Equal("Longhouse", kept.Name);
        Assert.Equal("Two floors", kept.Description);
        Assert.Equal(["stone", "Viking", "dungeon"], kept.Tags);
        Assert.True(kept.Dungeon);
        Assert.Empty(owner.OwnedWindows);

        // A dungeon's details: the switch on; off, the tag goes.
        asked = Dialogs.AskBlueprint(owner, new("Crypt", "", ["dungeon", "generated"]), "nothing");
        d = TestWindows.Shown(owner);
        dungeon = Assert.Single(d.GetLogicalDescendants().OfType<CheckBox>());
        Assert.True(dungeon.IsChecked);
        dungeon.IsChecked = false;
        Press(d, "Save");
        kept = await asked;
        Assert.Equal(["generated"], kept!.Tags);

        // Boxes emptied to nothing: empty details.
        asked = Dialogs.AskBlueprint(owner, new("Hut", "", []), "nothing");
        d = TestWindows.Shown(owner);
        foreach (var box in d.GetLogicalDescendants().OfType<TextBox>())
        {
            box.Text = null;
        }
        Press(d, "Save");
        kept = await asked;
        Assert.NotNull(kept);
        Assert.Equal(("", ""), (kept.Name, kept.Description));
        Assert.Empty(kept.Tags);

        asked = Dialogs.AskBlueprint(owner, new("Hut", "", []), "nothing");
        Press(TestWindows.Shown(owner), "Cancel");
        Assert.Null(await asked);
    }
}
