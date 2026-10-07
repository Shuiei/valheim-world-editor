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
		ok.Click += (_, _) => { result = true; dialog.Close(); };
		buttons.Children.Add(ok);
		if (no != null)
		{
			var cancel = new Button { Content = no, IsCancel = true };
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
}
