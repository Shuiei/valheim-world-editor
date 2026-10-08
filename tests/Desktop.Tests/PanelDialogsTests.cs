using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

// The small modal windows (Dialogs): a question answered with either button, a message with only
// OK, and a line of text typed, kept or cancelled. They are real windows owned by the main one: the
// tests find them there and press their buttons.
public class PanelDialogsTests
{
	// The dialog the owner shows (after the posted work that opens it).
	internal static Window Shown(Window owner)
	{
		Dispatcher.UIThread.RunJobs();
		return Assert.Single(owner.OwnedWindows);
	}

	internal static void Press(Window dialog, string label)
	{
		var b = dialog.GetLogicalDescendants().OfType<Button>().Single(b => b.Content as string == label);
		b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
		Dispatcher.UIThread.RunJobs();
	}

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
		var d = Shown(owner);
		Assert.Equal("Discard", d.Title);
		Assert.Contains("Discard the changes?", d.GetLogicalDescendants().OfType<SelectableTextBlock>().Single().Text);
		Press(d, "Discard");
		Assert.True(await asked);
		Assert.Empty(owner.OwnedWindows);

		asked = Dialogs.Ask(owner, "Discard", "Again?", "Discard", "Keep them");
		Press(Shown(owner), "Keep them");
		Assert.False(await asked);
	}

	[AvaloniaFact]
	public async Task AMessageHasOnlyOk()
	{
		var owner = Owner();
		var told = Dialogs.Tell(owner, "Save", "Saved 3 zone(s).");
		var d = Shown(owner);
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
		Shown(owner).Close();
		Dispatcher.UIThread.RunJobs();
		Assert.False(await asked);
	}

	[AvaloniaFact]
	public async Task ALineOfTextIsTypedKeptOrCancelled()
	{
		var owner = Owner();
		var typed = Dialogs.AskText(owner, "Save blueprint", "Name of the blueprint:", "My house");
		var d = Shown(owner);
		var box = d.GetLogicalDescendants().OfType<TextBox>().Single();
		Assert.Equal("My house", box.Text);
		box.Text = "Longhouse";
		Press(d, "OK");
		Assert.Equal("Longhouse", await typed);

		typed = Dialogs.AskText(owner, "Save blueprint", "Name:");
		d = Shown(owner);
		Assert.Equal("", d.GetLogicalDescendants().OfType<TextBox>().Single().Text);
		Press(d, "Cancel");
		Assert.Null(await typed);
	}
}
