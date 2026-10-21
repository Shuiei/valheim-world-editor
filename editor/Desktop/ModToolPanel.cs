using System.Globalization;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using TerrainEditor.App;
using ValheimWorldEditor.Sdk;

namespace TerrainEditor.Desktop;

/// <summary>
/// The card of a mod's own tool (ModExtensions.Tool), next to the rail: its name and help, then its controls as the
/// tool lists them (sliders, choices, toggles, buttons, lines of text), built again for each tool chosen. The values are
/// remembered between runs (Prefs, "mod.&lt;tool&gt;.&lt;control&gt;").
/// </summary>
public sealed class ModToolPanel
{
    private readonly Prefs? _prefs;
    private readonly StackPanel _body = new() { Width = 280, Spacing = 6 };
    private readonly Dictionary<string, float> _numbers = [];
    private readonly Dictionary<string, int> _choices = [];
    private readonly Dictionary<string, bool> _toggles = [];
    private readonly Dictionary<string, TextBlock> _texts = [];

    /// <summary>An empty card.</summary>
    /// <param name="prefs">Where the values are remembered (null: not remembered).</param>
    public ModToolPanel(Prefs? prefs)
    {
        _prefs = prefs;
        Card = Ui.AsCard(new Border { VerticalAlignment = VerticalAlignment.Top, Child = _body });
    }

    /// <summary>The card.</summary>
    public Control Card { get; }

    /// <summary>The tool shown, null before one is.</summary>
    public ModExtensions.Tool? Tool { get; private set; }

    /// <summary>A control was changed or a button pressed: its id.</summary>
    public event Action<string>? Changed;

    /// <summary>A slider's value (its first value, or its lowest, for one the card does not have).</summary>
    public float Number(string id) => _numbers.GetValueOrDefault(id);

    /// <summary>A choice's index (0 for one the card does not have).</summary>
    public int Choice(string id) => _choices.GetValueOrDefault(id);

    /// <summary>A toggle's value (false for one the card does not have).</summary>
    public bool IsOn(string id) => _toggles.GetValueOrDefault(id);

    /// <summary>Changes a text line of the card (nothing for one it does not have).</summary>
    public void SetText(string id, string text)
    {
        if (_texts.TryGetValue(id, out var t))
        {
            t.Text = text;
        }
    }

    /// <summary>Builds the card for a tool, its controls with the values remembered for it.</summary>
    /// <param name="tool">The tool.</param>
    public void Show(ModExtensions.Tool tool)
    {
        if (ReferenceEquals(Tool, tool))
        {
            return;
        }
        Tool = tool;
        _body.Children.Clear();
        _numbers.Clear();
        _choices.Clear();
        _toggles.Clear();
        _texts.Clear();
        _body.Children.Add(new TextBlock { Text = tool.Info.Name, FontSize = 14, FontWeight = FontWeight.SemiBold });
        _body.Children.Add(new TextBlock { Text = tool.Info.Help, FontSize = 11, Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap });
        foreach (var c in tool.Controls)
        {
            var made = Make(c);
            if (c.Help != null)
            {
                ToolTip.SetTip(made, c.Help);
            }
            _body.Children.Add(made);
        }
        _body.Children.Add(new TextBlock { Text = Lang.T("mods.tool.from", tool.Mod), FontSize = 10, Foreground = Ui.Muted });
    }

    /// <summary>One control on the card.</summary>
    private Control Make(ToolControl c) => c switch
    {
        SliderControl s => Slider(s),
        ChoiceControl ch => Choices(ch),
        ToggleControl t => Toggle(t),
        ButtonControl b => Button(b),
        _ => Text((TextControl)c),
    };

    private string Key(string id) => "mod." + Tool!.Name + "." + id;

    private Grid Slider(SliderControl s)
    {
        float value = Math.Clamp(_prefs?.Get(Key(s.Id), s.Value) ?? s.Value, s.Min, s.Max);
        _numbers[s.Id] = value;
        var shown = new TextBlock();
        string Format(double v) => v.ToString(s.Step >= 1 || s.Step == 0 && s.Max - s.Min > 50 ? "0" : "0.##", CultureInfo.InvariantCulture) + (s.Unit != null ? " " + s.Unit : "");
        var slider = Ui.Slide(s.Min, s.Max, s.Step > 0 ? s.Step : (s.Max - s.Min) / 100, value, shown, Format, v =>
        {
            _numbers[s.Id] = (float)v;
            _prefs?.Set(Key(s.Id), (float)v);
            Changed?.Invoke(s.Id);
        });
        slider.IsSnapToTickEnabled = s.Step > 0;
        return Ui.Row(s.Label, slider, shown);
    }

    private Grid Choices(ChoiceControl ch)
    {
        int value = Math.Clamp(_prefs?.Get(Key(ch.Id), 0) ?? 0, 0, ch.Options.Count - 1);
        _choices[ch.Id] = value;
        var box = new ComboBox { ItemsSource = ch.Options, SelectedIndex = value, FontSize = 12, HorizontalAlignment = HorizontalAlignment.Stretch };
        box.SelectionChanged += (_, _) =>
        {
            _choices[ch.Id] = Math.Max(0, box.SelectedIndex);
            _prefs?.Set(Key(ch.Id), _choices[ch.Id]);
            Changed?.Invoke(ch.Id);
        };
        return Ui.Row(ch.Label, box, null, "70,*,0");
    }

    private CheckBox Toggle(ToggleControl t)
    {
        bool value = _prefs?.Get(Key(t.Id), t.Value) ?? t.Value;
        _toggles[t.Id] = value;
        var box = new CheckBox { Content = t.Label, IsChecked = value, FontSize = 12 };
        box.IsCheckedChanged += (_, _) =>
        {
            _toggles[t.Id] = box.IsChecked == true;
            _prefs?.Set(Key(t.Id), _toggles[t.Id]);
            Changed?.Invoke(t.Id);
        };
        return box;
    }

    private Button Button(ButtonControl b)
    {
        var button = new Button { Content = b.Label, FontSize = 12, HorizontalAlignment = HorizontalAlignment.Stretch };
        button.Click += (_, _) => Changed?.Invoke(b.Id);
        return button;
    }

    private TextBlock Text(TextControl t)
    {
        var block = new TextBlock { Text = t.Label, FontSize = 12, TextWrapping = TextWrapping.Wrap };
        _texts[t.Id] = block;
        return block;
    }
}
