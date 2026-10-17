using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;

/// <summary>One source line's coverage: how many times it ran, and of its branches (none for a plain line), how many
/// were taken.</summary>
/// <param name="Hits">How many times the line ran.</param>
/// <param name="Taken">How many of its branches were taken.</param>
/// <param name="Branches">How many branches it has (0: none).</param>
internal sealed partial record Line(long Hits, int Taken, int Branches)
{
    /// <summary>The line as a report has it: its hits, and its branches from its condition-coverage ("50% (1/2)").</summary>
    /// <param name="line">The report's line element.</param>
    /// <returns>The line's coverage.</returns>
    public static Line Read(XElement line)
    {
        long hits = (long)line.Attribute("hits")!;
        Match m = Condition().Match((string?)line.Attribute("condition-coverage") ?? "");
        return m.Success
            ? new Line(hits, int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture), int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture))
            : new Line(hits, 0, 0);
    }

    /// <summary>This line merged with the same line in another report: the hits added up, the branches all taken
    /// when either took them all, else the most either took.</summary>
    /// <param name="other">The same line in another report.</param>
    /// <returns>The merged line.</returns>
    public Line With(Line other)
    {
        int branches = Math.Max(Branches, other.Branches);
        bool all = (Branches > 0 && Taken == Branches) || (other.Branches > 0 && other.Taken == other.Branches);
        return new Line(Hits + other.Hits, all ? branches : Math.Max(Taken, other.Taken), branches);
    }

    /// <summary>The line as the merged report writes it.</summary>
    /// <param name="number">Its line number.</param>
    /// <returns>The line element.</returns>
    public XElement Write(int number)
    {
        var line = new XElement("line", new XAttribute("number", number), new XAttribute("hits", Hits));
        if (Branches == 0)
        {
            line.Add(new XAttribute("branch", "False"));
            return line;
        }
        int percent = 100 * Taken / Branches;
        line.Add(
            new XAttribute("branch", "True"),
            new XAttribute("condition-coverage", $"{percent}% ({Taken}/{Branches})"));
        return line;
    }

    [GeneratedRegex(@"\((\d+)/(\d+)\)")]
    private static partial Regex Condition();
}
