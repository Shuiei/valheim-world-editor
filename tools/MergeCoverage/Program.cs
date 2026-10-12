using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;

// Merges Cobertura reports (the test containers' coverage, as Microsoft's collector writes them) into one, for
// Codecov:
//   MergeCoverage <merged.xml> <report.xml>...
// Each source line's hits are added up over the reports. A line with branches counts all of them taken when one
// report took them all, else the most any one report took: Cobertura does not say which branches a report took,
// so two reports that each took half are not counted as all (never more than a report shows). Microsoft's
// dotnet-coverage merge was not used: it drops the branch counts of the lines it merges (Codecov then shows every
// line with branches as fully run), and its merge of the collector's own format lost lines that one report ran.
// Prints the merged totals: the lines covered as Codecov counts them (run, every branch on them taken), which its
// check compares with its target, and the lines run at all.
if (args.Length < 2)
{
    Console.Error.WriteLine("usage: MergeCoverage <merged.xml> <report.xml>...");
    return 2;
}

var files = new SortedDictionary<string, SortedDictionary<int, Line>>(StringComparer.Ordinal);
foreach (string report in args[1..])
{
    foreach (XElement cls in XDocument.Load(report).Descendants("class"))
    {
        string file = (string?)cls.Attribute("filename") ?? "";
        if (!files.TryGetValue(file, out SortedDictionary<int, Line>? lines))
        {
            lines = [];
            files[file] = lines;
        }
        // The class's own lines (each of its methods lists its lines again).
        foreach (XElement line in cls.Elements("lines").Elements("line"))
        {
            int number = (int)line.Attribute("number")!;
            var read = Line.Read(line);
            lines[number] = lines.TryGetValue(number, out Line? had) ? had.With(read) : read;
        }
    }
}

static string Rate(int part, int whole) => (whole == 0 ? 1.0 : (double)part / whole).ToString(CultureInfo.InvariantCulture);

// Cobertura's DTD requires line-rate, branch-rate and complexity on the coverage, each package and each class; the
// reports carry no complexity worth adding up, so it is written as 0.
int linesValid = 0, linesCovered = 0, linesFull = 0, branchesValid = 0, branchesCovered = 0;
var classes = new XElement("classes");
foreach ((string file, SortedDictionary<int, Line> lines) in files)
{
    var written = new XElement("lines");
    int fileValid = 0, fileCovered = 0, fileBranchesValid = 0, fileBranchesCovered = 0;
    foreach ((int number, Line line) in lines)
    {
        fileValid++;
        if (line.Hits > 0)
        {
            fileCovered++;
            // As Codecov counts a line covered: run, and every branch on it taken.
            if (line.Taken == line.Branches)
            {
                linesFull++;
            }
        }
        fileBranchesValid += line.Branches;
        fileBranchesCovered += line.Taken;
        written.Add(line.Write(number));
    }
    linesValid += fileValid;
    linesCovered += fileCovered;
    branchesValid += fileBranchesValid;
    branchesCovered += fileBranchesCovered;
    classes.Add(new XElement(
        "class",
        new XAttribute("name", file),
        new XAttribute("filename", file),
        new XAttribute("line-rate", Rate(fileCovered, fileValid)),
        new XAttribute("branch-rate", Rate(fileBranchesCovered, fileBranchesValid)),
        new XAttribute("complexity", 0),
        new XElement("methods"),
        written));
}

var merged = new XDocument(
    new XElement(
        "coverage",
        new XAttribute("line-rate", Rate(linesCovered, linesValid)),
        new XAttribute("branch-rate", Rate(branchesCovered, branchesValid)),
        new XAttribute("complexity", 0),
        new XAttribute("version", "1.9"),
        new XAttribute("timestamp", DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
        new XAttribute("lines-covered", linesCovered),
        new XAttribute("lines-valid", linesValid),
        new XAttribute("branches-covered", branchesCovered),
        new XAttribute("branches-valid", branchesValid),
        new XElement("packages", new XElement(
            "package",
            new XAttribute("name", "merged"),
            new XAttribute("line-rate", Rate(linesCovered, linesValid)),
            new XAttribute("branch-rate", Rate(branchesCovered, branchesValid)),
            new XAttribute("complexity", 0),
            classes))));
merged.Save(args[0]);
Console.WriteLine($"Merged {args.Length - 1} report(s): {linesFull} of {linesValid} lines covered as Codecov counts them ({100.0 * linesFull / Math.Max(linesValid, 1):F2}%: run, every branch taken); "
    + $"{linesCovered} run at all ({100.0 * linesCovered / Math.Max(linesValid, 1):F2}%); {branchesCovered} of {branchesValid} branches taken.");
return 0;

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
