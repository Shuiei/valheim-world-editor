using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.LogicalTree;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class ClaudeTools
{
    /// <summary>A control's kind as Claude names it, or null when it is not one Claude sets.</summary>
    internal static string? KindOf(Control c) => c switch
    {
        CheckBox => "switch",
        RadioButton => "radio",
        ToggleButton => "toggle",
        Button => "button",
        ComboBox => "dropdown",
        Slider => "slider",
        NumericUpDown => "number",
        TextBox => "text",
        _ => null,
    };

    /// <summary>Text shown by a control's content (a string, a text block, or the first text in a panel).</summary>
    private static string? TextOf(object? content) => content switch
    {
        string s => s,
        TextBlock t => t.Text,
        Control c => c.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text).FirstOrDefault(t => !string.IsNullOrWhiteSpace(t)),
        _ => null,
    };

    /// <summary>
    /// A control's words: its content or placeholder, else the text block just before it in its panel, else its tip's
    /// first sentence.
    /// </summary>
    internal static string LabelOf(Control c)
    {
        string? text = c switch
        {
            ContentControl cc => TextOf(cc.Content),
            TextBox t => t.PlaceholderText,
            _ => null,
        };
        if (string.IsNullOrWhiteSpace(text) && c.Parent is Panel p)
        {
            int i = p.Children.IndexOf(c);
            text = p.Children.Take(Math.Max(0, i)).Reverse().OfType<TextBlock>().Select(t => t.Text).FirstOrDefault(t => !string.IsNullOrWhiteSpace(t));
        }
        if (string.IsNullOrWhiteSpace(text) && ToolTip.GetTip(c) is string tip)
        {
            text = tip.Split(". ")[0];
        }
        return (text ?? "").Trim();
    }

    /// <summary>The window's controls Claude can set, in the window's order, with ids (stable while the window's layout is).</summary>
    internal static List<Ctl> ControlsOf(Control root)
    {
        var list = new List<Ctl>();
        var seen = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var c in root.GetLogicalDescendants().OfType<Control>())
        {
            if (KindOf(c) is not string kind)
            {
                continue;
            }
            string label = LabelOf(c);
            // From the English label: the same id whatever the language (and not lost to the letters a-z keeps).
            string english = Lang.EnglishOf(label);
            string id = Tips.KeyOf(c) ?? "~" + (english.Length > 0 ? Regex.Replace(english.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-') : kind);
            int k = seen[id] = seen.GetValueOrDefault(id) + 1;
            list.Add(new Ctl(k == 1 ? id : $"{id}#{k}", kind, label, c));
        }
        return list;
    }
}
