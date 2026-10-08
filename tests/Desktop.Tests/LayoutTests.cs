using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

// In a short window (900 px is common: 1080p at 125%), the panels over the 3D view stop above the
// status bar and scroll instead of running under it.
public class LayoutTests
{
	[AvaloniaTheory]
	[InlineData(700)]
	[InlineData(900)]
	public void ThePanelsStopAboveTheStatusBarAndScroll(int height)
	{
		var w = new MainWindow(load: false) { Width = 1440, Height = height };
		w.Show();
		var s = EditTests.Flat(2);
		w.View.Show(s.Scene, null);
		w.Edit(s);
		var view = (Border)typeof(MainWindow).GetField("_viewPanel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(w)!;
		w.ShowRight(view);
		w.Tools.ChooseMode(ToolMode.Area);
		w.UpdateLayout();
		Assert.IsType<ScrollViewer>(view.Child);
		Assert.True(view.Bounds.Bottom <= height - 58 + 0.5, $"View panel ends at {view.Bounds.Bottom} of {height}");
		// Every visible card of the tool column too.
		var column = w.Tools.Rail.GetVisualParent()!;
		foreach (var card in column.GetVisualChildren().OfType<Border>().Where(c => c.IsVisible))
		{
			Assert.IsType<ScrollViewer>(card.Child);
			Assert.True(card.TranslatePoint(new Point(0, card.Bounds.Height), w)!.Value.Y <= height - 58 + 0.5);
		}
	}
}
