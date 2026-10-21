using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

// Mods' own tools on the rail (ModExtensions.Tools): a section of their own, last.
public sealed partial class ToolPanel
{
    /// <summary>The rail's Mods section: a caption, then a button per mod's tool (empty: nothing shows).</summary>
    private readonly StackPanel _modRail = new() { Spacing = 0 };

    /// <summary>Each mod's tool's button, by its name among the tools.</summary>
    private readonly Dictionary<string, Button> _modButtons = [];

    /// <summary>The mod's tool in use (with <see cref="ToolMode.Mod"/>), null otherwise.</summary>
    public ModExtensions.Tool? ModTool { get; private set; }

    /// <summary>The mods' tools on the rail.</summary>
    public IReadOnlyList<ModExtensions.Tool> ModTools { get; private set; } = [];

    /// <summary>
    /// Puts the running extensions' tools on the rail (as a world or area opens); the one in use, gone now, gives way to
    /// View.
    /// </summary>
    /// <param name="tools">The tools.</param>
    public void SetModTools(IReadOnlyList<ModExtensions.Tool> tools)
    {
        ModTools = tools;
        _modRail.Children.Clear();
        _modButtons.Clear();
        if (tools.Count > 0)
        {
            _modRail.Children.Add(new TextBlock { Text = Lang.T("rail.cap.mods").ToUpperInvariant(), FontSize = 9, Foreground = Ui.Muted, LetterSpacing = 0.7, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 5, 0, 1) });
        }
        foreach (var t in tools)
        {
            Control picture = TryIcon(t.Info.Icon) ?? new TextBlock { Text = t.Info.Name[..1].ToUpperInvariant(), FontSize = 13, FontWeight = FontWeight.SemiBold, HorizontalAlignment = HorizontalAlignment.Center };
            var b = RailButton(t.Info.Name, "", picture);
            ToolTip.SetTip(b, t.Info.Name + " (" + t.Mod + ")\n" + t.Info.Help);
            b.Click += (_, _) => ChooseMod(t);
            _modRail.Children.Add(b);
            _modButtons[t.Name] = b;
        }
        if (Mode == ToolMode.Mod && (ModTool == null || !_modButtons.ContainsKey(ModTool.Name)))
        {
            Choose(null);
        }
    }

    /// <summary>A mod's tool's icon from its path data; null when there is none or it cannot be read (logged).</summary>
    private static Control? TryIcon(string? data)
    {
        if (string.IsNullOrWhiteSpace(data))
        {
            return null;
        }
        try
        {
            var icon = Icons.FromPath(data, 16);
            icon.HorizontalAlignment = HorizontalAlignment.Center;
            return icon;
        }
        catch (FormatException e)
        {
            Log.Caught(e, "reading a mod's tool's icon (its first letter is shown instead)");
            return null;
        }
    }

    /// <summary>Chooses a mod's tool.</summary>
    /// <param name="tool">The tool.</param>
    public void ChooseMod(ModExtensions.Tool tool)
    {
        if (Workshop)
        {
            return;
        }
        ModTool = tool;
        Choose(null, ToolMode.Mod);
    }

    /// <summary>The "on" look on the mod's tool in use, off on the others.</summary>
    private void MarkModTool()
    {
        foreach (var (name, b) in _modButtons)
        {
            b.Classes.Set("on", Mode == ToolMode.Mod && ModTool?.Name == name);
        }
    }

    /// <summary>A rail button: its picture, the name under it and the key in its corner.</summary>
    private static Button RailButton(string label, string key, Control picture)
    {
        picture.HorizontalAlignment = HorizontalAlignment.Center;
        return new Button
        {
            Content = new Grid
            {
                Children =
                {
                    new StackPanel { Spacing = 1, Children = { picture, new TextBlock { Text = label, FontSize = 10.5, HorizontalAlignment = HorizontalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis } } },
                    new TextBlock { Text = key, FontSize = 9, Opacity = 0.6, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, -2, -1, 0), FontFamily = new FontFamily("monospace") },
                },
            },
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(2, 3, 3, 2),
        }.Classed("rail");
    }
}
