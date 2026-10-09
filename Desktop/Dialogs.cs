using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace TerrainEditor.Desktop;

// Small modal questions and messages (Avalonia has no message box of its own).
public static class Dialogs
{
	// True when the first button is chosen.
	public static async Task<bool> Ask(Window owner, string title, string text, string yes, string? no = "Cancel")
	{
		bool result = false;
		var dialog = new Window
		{
			Title = title,
			Width = 560,
			SizeToContent = SizeToContent.Height,
			CanResize = false,
			WindowStartupLocation = WindowStartupLocation.CenterOwner,
			Background = new SolidColorBrush(Color.FromRgb(24, 28, 34)),
		};
		var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
		var ok = new Button { Content = yes, IsDefault = true };
		ToolTip.SetTip(ok, $"{yes} (Enter).");
		ok.Click += (_, _) => { result = true; dialog.Close(); };
		buttons.Children.Add(ok);
		if (no != null)
		{
			var cancel = new Button { Content = no, IsCancel = true }.Tip("dialog.cancel");
			cancel.Click += (_, _) => dialog.Close();
			buttons.Children.Add(cancel);
		}
		dialog.Content = new StackPanel
		{
			Margin = new Thickness(18),
			Spacing = 16,
			Children = { new SelectableTextBlock { Text = text, TextWrapping = TextWrapping.Wrap, FontSize = 13 }, buttons },
		};
		await dialog.ShowDialog(owner);
		return result;
	}

	public static Task Tell(Window owner, string title, string text) => Ask(owner, title, text, "OK", null);

	// A line of text (null: cancelled).
	public static async Task<string?> AskText(Window owner, string title, string text, string initial = "")
	{
		string? result = null;
		var dialog = new Window
		{
			Title = title,
			Width = 420,
			SizeToContent = SizeToContent.Height,
			CanResize = false,
			WindowStartupLocation = WindowStartupLocation.CenterOwner,
			Background = new SolidColorBrush(Color.FromRgb(24, 28, 34)),
		};
		var box = new TextBox { Text = initial }.Tip("dialog.text");
		var ok = new Button { Content = "OK", IsDefault = true };
		ToolTip.SetTip(ok, "Use this answer (Enter).");
		ok.Click += (_, _) => { result = box.Text; dialog.Close(); };
		var cancel = new Button { Content = "Cancel", IsCancel = true }.Tip("dialog.cancel");
		cancel.Click += (_, _) => dialog.Close();
		dialog.Content = new StackPanel
		{
			Margin = new Thickness(18),
			Spacing = 12,
			Children = { new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, FontSize = 13 }, box,
				new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Children = { ok, cancel } } },
		};
		dialog.Opened += (_, _) => box.Focus();
		await dialog.ShowDialog(owner);
		return result;
	}

	// A blueprint's name, description and tags (comma separated), with what it costs to build; null
	// when cancelled.
	public static async Task<TerrainEditor.App.Homestead.Details?> AskBlueprint(Window owner, TerrainEditor.App.Homestead.Details start, string cost)
	{
		TerrainEditor.App.Homestead.Details? result = null;
		var dialog = new Window
		{
			Title = "Blueprint",
			Width = 460,
			SizeToContent = SizeToContent.Height,
			CanResize = false,
			WindowStartupLocation = WindowStartupLocation.CenterOwner,
			Background = new SolidColorBrush(Color.FromRgb(24, 28, 34)),
		};
		var name = new TextBox { Text = start.Name }.Tip("dialog.blueprintName");
		var description = new TextBox { Text = start.Description, AcceptsReturn = false, TextWrapping = TextWrapping.Wrap, MinHeight = 54, PlaceholderText = "What it is, how to build it…" }.Tip("dialog.blueprintDescription");
		var tags = new TextBox { Text = string.Join(", ", start.Tags), PlaceholderText = "house, viking, stone" }.Tip("dialog.blueprintTags");
		var ok = new Button { Content = "Save", IsDefault = true };
		ToolTip.SetTip(ok, "Keep these details (Enter).");
		ok.Click += (_, _) =>
		{
			result = new TerrainEditor.App.Homestead.Details(name.Text ?? "", (description.Text ?? "").Replace('\n', ' ').Replace('\r', ' '), TerrainEditor.App.Homestead.Details.ParseTags(tags.Text));
			dialog.Close();
		};
		var cancel = new Button { Content = "Cancel", IsCancel = true }.Tip("dialog.cancel");
		cancel.Click += (_, _) => dialog.Close();
		TextBlock Label(string t) => new() { Text = t, FontSize = 12, Foreground = Brushes.Gray };
		dialog.Content = new StackPanel
		{
			Margin = new Thickness(18),
			Spacing = 6,
			Children =
			{
				Label("Name"), name,
				Label("Description"), description,
				Label("Tags (separated by commas)"), tags,
				new TextBlock { Text = "Cost in game: " + cost, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0) },
				new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 8, 0, 0), Children = { ok, cancel } },
			},
		};
		dialog.Opened += (_, _) => name.Focus();
		await dialog.ShowDialog(owner);
		return result;
	}
}
