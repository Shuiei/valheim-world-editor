using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Shapes;
using Avalonia.Media;

namespace TerrainEditor.Desktop;

// The web editor's line icons (24 × 24 SVG paths, drawn with a 1.8 px stroke in the text colour), so
// both editors look alike.
public static class Icons
{
	public static readonly Dictionary<string, string> Paths = new()
	{
		["raise"] = "M12 19V5M6 11l6-6 6 6",
		["lower"] = "M12 5v14M6 13l6 6 6-6",
		["flatten"] = "M3 15h18M6 11h12M9 7h6",
		["smooth"] = "M3 14c3-4 6 4 9 0s6 4 9 0",
		["natural"] = "M3 18l5-8 4 5 3-4 6 7z",
		["restore"] = "M4 12a8 8 0 1 0 8-8M4 4v5h5",
		["erode"] = "M3 6c3 0 4 3 7 3s4-3 7-3 4 2 4 2M3 12c3 0 4 3 7 3s4-3 7-3M8 18l2 3 2-3",
		["shape"] = "M3 19l6-11 4 6 2-3 6 8z",
		["mountain"] = "M2 20l7-14 4 6 3-4 6 12zM7 10l2 2 2-2",
		["cave"] = "M3 20v-8a9 8 0 0 1 18 0v8M8 20v-5a4 4 0 0 1 8 0v5",
		["path"] = "M4 19c4 0 3-7 8-7s4-7 8-7M4 19h.01M20 5h.01",
		["area"] = "M4 4h4M16 4h4v4M20 16v4h-4M8 20H4v-4M4 8v4M20 8v4",
		["paste"] = "M8 4h8v3H8zM6 6H5v14h14V6h-1",
		["place"] = "M12 21v-7M12 14c-4 0-6-3-6-6 3 0 6 2 6 6zM12 12c0-4 2-7 6-7 0 4-2 7-6 7z",
		["measure"] = "M3 17L17 3l4 4L7 21zM7 13l2 2M10 10l2 2M13 7l2 2",
		["select"] = "M5 3l14 8-6 2-2 6z",
		["move"] = "M12 3v18M3 12h18M12 3l-3 3M12 3l3 3M12 21l-3-3M12 21l3-3M3 12l3-3M3 12l3 3M21 12l-3-3M21 12l-3 3",
		["back"] = "M15 5l-7 7 7 7",
		["undo"] = "M9 14l-5-5 5-5M4 9h10a6 6 0 0 1 0 12h-3",
		["redo"] = "M15 14l5-5-5-5M20 9H10a6 6 0 0 0 0 12h3",
		["history"] = "M12 3a9 9 0 1 0 0 18a9 9 0 1 0 0-18M12 7v5l3 2",
		["view"] = "M2 12s3.5-7 10-7 10 7 10 7-3.5 7-10 7S2 12 2 12zM12 9a3 3 0 1 0 0 6a3 3 0 1 0 0-6",
		["help"] = "M12 3a9 9 0 1 0 0 18a9 9 0 1 0 0-18M9.5 9.5a2.5 2.5 0 0 1 5 .5c0 1.7-2.5 2-2.5 3.5M12 17h.01",
		["map"] = "M3 6l6-2 6 2 6-2v14l-6 2-6-2-6 2zM9 4v14M15 6v14",
		["search"] = "M11 4a7 7 0 1 0 0 14a7 7 0 1 0 0-14M21 21l-5-5",
		["save"] = "M5 3h11l3 3v15H5zM8 3v6h8V3M8 21v-7h8v7",
		["close"] = "M6 6l12 12M18 6L6 18",
		["folder"] = "M3 6h6l2 2h10v11H3z",
		["server"] = "M4 4h16v6H4zM4 14h16v6H4zM8 7h.01M8 17h.01",
		["game"] = "M6 9h12a4 4 0 0 1 0 8l-2-2H8l-2 2a4 4 0 0 1 0-8zM8 12v2M7 13h2M15 12h.01M17 14h.01",
		["settings"] = "M12 9a3 3 0 1 0 0 6a3 3 0 1 0 0-6M12 2v3M12 19v3M4.2 4.2l2.1 2.1M17.7 17.7l2.1 2.1M2 12h3M19 12h3M4.2 19.8l2.1-2.1M17.7 6.3l2.1-2.1",
		["globe"] = "M12 3a9 9 0 1 0 0 18a9 9 0 1 0 0-18M3 12h18M12 3c3 3 3 15 0 18M12 3c-3 3-3 15 0 18",
		["reload"] = "M20 12a8 8 0 1 1-3-6.2M20 4v5h-5",
		["trash"] = "M4 7h16M9 7V4h6v3M6 7l1 13h10l1-13",
	};

	// An icon in the colour of the text around it (a button's, for example).
	public static Control Make(string name, double size = 18, double stroke = 1.8)
	{
		var path = new Avalonia.Controls.Shapes.Path
		{
			Data = Geometry.Parse(Paths[name]),
			StrokeThickness = stroke,
			StrokeLineCap = PenLineCap.Round,
			StrokeJoin = PenLineJoin.Round,
			Width = 24,
			Height = 24,
		};
		path.Bind(Shape.StrokeProperty, path.GetObservable(TextElement.ForegroundProperty));
		return new Viewbox { Width = size, Height = size, Child = path, IsHitTestVisible = false };
	}

	// An icon and a label side by side (button content).
	public static Control With(string name, string text, double size = 16) => new StackPanel
	{
		Orientation = Avalonia.Layout.Orientation.Horizontal,
		Spacing = 6,
		Children = { Make(name, size), new TextBlock { Text = text, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center } },
	};
}
