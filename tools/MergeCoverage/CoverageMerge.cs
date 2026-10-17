using System.Globalization;
using System.Xml.Linq;

/// <summary>
/// Reads the reports' lines by file, and writes them as one Cobertura report with the totals counted for Codecov.
/// </summary>
internal static class CoverageMerge
{
    /// <summary>
    /// The lines of every file in the reports, each file's lines by number, the reports' hits added up.
    /// </summary>
    /// <param name="reports">The report files.</param>
    /// <returns>The lines by file name, then by line number.</returns>
    public static SortedDictionary<string, SortedDictionary<int, Line>> ReadReports(IEnumerable<string> reports)
    {
        var files = new SortedDictionary<string, SortedDictionary<int, Line>>(StringComparer.Ordinal);
        foreach (string report in reports)
        {
            foreach (XElement cls in XDocument.Load(report).Descendants("class"))
            {
                AddClass(files, cls);
            }
        }
        return files;
    }

    /// <summary>
    /// Adds one class's lines to the file they are in (the class's own lines: each of its methods lists its lines again).
    /// </summary>
    /// <param name="files">The lines by file name, then by line number.</param>
    /// <param name="cls">The report's class element.</param>
    private static void AddClass(SortedDictionary<string, SortedDictionary<int, Line>> files, XElement cls)
    {
        string file = (string?)cls.Attribute("filename") ?? "";
        if (!files.TryGetValue(file, out SortedDictionary<int, Line>? lines))
        {
            lines = [];
            files[file] = lines;
        }
        foreach (XElement line in cls.Elements("lines").Elements("line"))
        {
            int number = (int)line.Attribute("number")!;
            var read = Line.Read(line);
            lines[number] = lines.TryGetValue(number, out Line? had) ? had.With(read) : read;
        }
    }

    /// <summary>
    /// The merged report and its totals: each file as a class, the totals on the coverage and the package.
    /// </summary>
    /// <param name="files">The lines by file name, then by line number.</param>
    /// <returns>The merged document and the totals.</returns>
    public static (XDocument Document, Totals Totals) Merge(SortedDictionary<string, SortedDictionary<int, Line>> files)
    {
        var classes = new XElement("classes");
        Totals total = Totals.None;
        foreach ((string file, SortedDictionary<int, Line> lines) in files)
        {
            var (element, totals) = ClassOf(file, lines);
            classes.Add(element);
            total = total.Plus(totals);
        }
        return (Document(classes, total), total);
    }

    /// <summary>
    /// One file's class element, with its lines and the totals of those lines.
    /// </summary>
    /// <param name="file">The file's name.</param>
    /// <param name="lines">Its lines by number.</param>
    /// <returns>The class element and its totals.</returns>
    private static (XElement Element, Totals Totals) ClassOf(string file, SortedDictionary<int, Line> lines)
    {
        var written = new XElement("lines");
        Totals totals = Totals.None;
        foreach ((int number, Line line) in lines)
        {
            totals = totals.Plus(Totals.OfLine(line));
            written.Add(line.Write(number));
        }
        var element = new XElement(
            "class",
            new XAttribute("name", file),
            new XAttribute("filename", file),
            new XAttribute("line-rate", Rate(totals.LinesCovered, totals.LinesValid)),
            new XAttribute("branch-rate", Rate(totals.BranchesCovered, totals.BranchesValid)),
            new XAttribute("complexity", 0),
            new XElement("methods"),
            written);
        return (element, totals);
    }

    /// <summary>
    /// The whole report: the classes in a package, with the totals on the coverage and the package.
    /// </summary>
    /// <param name="classes">The class elements.</param>
    /// <param name="total">The totals of every class.</param>
    /// <returns>The Cobertura document.</returns>
    private static XDocument Document(XElement classes, Totals total) => new(
        new XElement(
            "coverage",
            new XAttribute("line-rate", Rate(total.LinesCovered, total.LinesValid)),
            new XAttribute("branch-rate", Rate(total.BranchesCovered, total.BranchesValid)),
            new XAttribute("complexity", 0),
            new XAttribute("version", "1.9"),
            new XAttribute("timestamp", DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
            new XAttribute("lines-covered", total.LinesCovered),
            new XAttribute("lines-valid", total.LinesValid),
            new XAttribute("branches-covered", total.BranchesCovered),
            new XAttribute("branches-valid", total.BranchesValid),
            new XElement("packages", new XElement(
                "package",
                new XAttribute("name", "merged"),
                new XAttribute("line-rate", Rate(total.LinesCovered, total.LinesValid)),
                new XAttribute("branch-rate", Rate(total.BranchesCovered, total.BranchesValid)),
                new XAttribute("complexity", 0),
                classes))));

    /// <summary>
    /// A part over a whole as a rate (a whole of 0 is a full rate).
    /// </summary>
    /// <param name="part">The part.</param>
    /// <param name="whole">The whole.</param>
    /// <returns>The rate, in invariant culture.</returns>
    private static string Rate(int part, int whole) => (whole == 0 ? 1.0 : (double)part / whole).ToString(CultureInfo.InvariantCulture);
}
