using Avalonia.Controls;
using Avalonia.Controls.Documents;

namespace TerrainEditor.Desktop.Tests;

internal sealed partial class PseudoLang
{
    /// <summary>
    /// The texts of a text block's runs: each run's text, the spans' runs, and the controls inside it (walked). A kind of
    /// inline not read here throws, so its text is not let through unchecked.
    /// </summary>
    /// <param name="inlines">The runs.</param>
    /// <param name="check">Checks one shown text.</param>
    /// <param name="walk">Walks a control inside the runs.</param>
    private static void RunTexts(InlineCollection inlines, Action<object?> check, Action<Control> walk)
    {
        foreach (var inline in inlines)
        {
            switch (inline)
            {
                case Run r:
                    check(r.Text);
                    break;
                case Span s:
                    RunTexts(s.Inlines, check, walk);
                    break;
                case InlineUIContainer container:
                    if (container.Child is { } child)
                    {
                        walk(child);
                    }
                    break;
                case LineBreak:
                    // No text.
                    break;
                default:
                    // A kind of inline not read here would let its text through unchecked.
                    throw new NotSupportedException($"PseudoLang cannot read a {inline.GetType().Name} in a text block.");
            }
        }
    }
}
