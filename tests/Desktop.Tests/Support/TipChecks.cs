using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The tip checks the Tips tests share: the controls under a root that have no tip, their description for a failure,
/// and the failure when any were found.
/// </summary>
internal static class TipChecks
{
    /// <summary>
    /// Controls left without a tip, with the reason. None today: even a dialog's answers say what they do and their
    /// key.
    /// </summary>
    private static bool Exempt(Control _) => false;

    /// <summary>
    /// The controls shown without a tip, described so they can be found in the code.
    /// </summary>
    private static List<string> Untipped(Control root)
    {
        var missing = new List<string>();
        foreach (var c in root.GetLogicalDescendants().OfType<Control>().Prepend(root))
        {
            if (c is not (Button or ComboBox or Slider or TextBox or NumericUpDown) || Exempt(c))
            {
                continue;
            }
            if (ToolTip.GetTip(c) is null or "")
            {
                missing.Add(Describe(c));
            }
        }
        return missing;
    }

    /// <summary>
    /// The text a control's content shows, through text blocks and panels.
    /// </summary>
    private static string Text(object? o) => o switch
    {
        string s => s,
        TextBlock t => t.Text ?? "",
        Panel p => string.Join(" ", p.Children.Select(Text).Where(t => t.Length > 0)),
        ContentControl cc => Text(cc.Content),
        _ => "",
    };

    /// <summary>
    /// A control described for the failure message: its kind, its text, the label beside it and its parents.
    /// </summary>
    private static string Describe(Control c)
    {
        string what = c switch
        {
            ContentControl cc => Text(cc.Content),
            TextBox t => t.PlaceholderText ?? t.Text ?? "",
            ComboBox b => b.SelectedItem?.ToString() ?? "",
            _ => "",
        };
        // The label beside it, when there is one.
        string label = "";
        if (c.Parent is Panel p)
        {
            int i = p.Children.IndexOf(c);
            label = string.Join(" | ", p.Children.Take(Math.Max(0, i)).OfType<TextBlock>().Select(t => t.Text));
        }
        return $"{c.GetType().Name} '{what}' (label: {label}) in {string.Join("/", c.GetLogicalAncestors().OfType<Control>().Take(4).Select(a => a.GetType().Name))}";
    }

    /// <summary>
    /// Adds the controls under <c>root</c> that have no tip, after the posted work is run.
    /// </summary>
    internal static void Check(HashSet<string> missing, Control root)
    {
        Dispatcher.UIThread.RunJobs();
        foreach (string m in Untipped(root))
        {
            missing.Add(m);
        }
    }

    /// <summary>
    /// Fails, listing them, when any control was found without a tip.
    /// </summary>
    internal static void NoneMissing(HashSet<string> missing) => Assert.True(missing.Count == 0, $"{missing.Count} control(s) without a tip:\n" + string.Join("\n", missing.OrderBy(m => m)));
}
