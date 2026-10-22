using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace TerrainEditor.Desktop;

public static partial class Ui
{
    /// <summary>A small "?" button opening the Help window at a part's documentation (HelpTopics).</summary>
    /// <param name="topic">Where, asked when clicked (a brush card's changes with its brush).</param>
    public static Button HelpButton(Func<HelpTarget> topic)
    {
        var b = new Button { Content = Icons.Make("help", 15), Padding = new Thickness(4), VerticalAlignment = VerticalAlignment.Center }.Classed("ghost").Tip("docs.topic");
        b.Click += (_, _) => HelpWindow.Open(topic());
        return b;
    }

    /// <summary>A panel's title with its help button at the right (Title of a card: the tool's name).</summary>
    /// <param name="text">The title.</param>
    /// <param name="topic">Its documentation.</param>
    public static Grid Title(string text, HelpTarget topic) => Titled(new TextBlock { Text = text }, () => topic);

    /// <summary>A panel's title block (given its title look) with a help button at the right.</summary>
    /// <param name="title">The title (its text may change).</param>
    /// <param name="topic">Its documentation, asked when the button is clicked.</param>
    public static Grid Titled(TextBlock title, Func<HelpTarget> topic)
    {
        title.FontSize = 14;
        title.FontWeight = FontWeight.SemiBold;
        title.VerticalAlignment = VerticalAlignment.Center;
        return new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { title, Col(HelpButton(topic), 1) } };
    }
}
