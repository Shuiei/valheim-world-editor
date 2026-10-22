using System.Text;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace TerrainEditor.Desktop;

/// <summary>The plain text of the documentation's Markdown: what a heading says, what search looks through.</summary>
public static class HelpText
{
    /// <summary>An inline run's text, marks left out ("**Map** (`M`)" is "Map (M)"); empty for none.</summary>
    /// <param name="inline">The run (a heading's or a paragraph's, a link's text, a picture's description).</param>
    public static string Of(ContainerInline? inline)
    {
        var sb = new StringBuilder();
        // Its children: a picture's own description is its text here (a picture within text has none).
        foreach (var child in inline ?? Enumerable.Empty<Inline>())
        {
            Append(sb, child);
        }
        return sb.ToString().Trim();
    }

    /// <summary>A block's text, its paragraphs, items and cells one after another, spaced.</summary>
    /// <param name="block">The block (a paragraph, a list, a table...).</param>
    public static string Of(Block block)
    {
        var sb = new StringBuilder();
        Append(sb, block);
        return sb.ToString().Trim();
    }

    /// <summary>Adds a block's text: its own inline run, or each of its children's.</summary>
    private static void Append(StringBuilder sb, Block block)
    {
        switch (block)
        {
            case LeafBlock { Inline: { } inline }:
                Append(sb, inline);
                sb.Append(' ');
                break;
            case LeafBlock leaf:
                // A code block: its lines.
                sb.Append(leaf.Lines.ToString()).Append(' ');
                break;
            case ContainerBlock container:
                foreach (var child in container)
                {
                    Append(sb, child);
                }
                break;
        }
    }

    /// <summary>Adds an inline run's text (a picture's description is not text: left out).</summary>
    private static void Append(StringBuilder sb, Inline? inline)
    {
        switch (inline)
        {
            case LiteralInline l:
                sb.Append(l.Content.AsSpan());
                break;
            case CodeInline c:
                sb.Append(c.Content);
                break;
            case LineBreakInline:
                sb.Append(' ');
                break;
            case LinkInline { IsImage: true }:
                break;
            case ContainerInline container:
                foreach (var child in container)
                {
                    Append(sb, child);
                }
                break;
        }
    }

    /// <summary>A heading's id (GitHub's: "walk-and-fly"), what a link's "#anchor" names; null when it has none.</summary>
    /// <param name="heading">The heading.</param>
    public static string? Id(HeadingBlock heading) => heading.TryGetAttributes()?.Id;
}
