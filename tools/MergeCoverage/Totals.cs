/// <summary>
/// The counts of a merged report: its lines (valid, run at all, and run with every branch taken) and its branches
/// (valid, and taken).
/// </summary>
/// <param name="LinesValid">The lines the report has.</param>
/// <param name="LinesCovered">The lines run at all.</param>
/// <param name="LinesFull">The lines run with every branch on them taken (as Codecov counts a line covered).</param>
/// <param name="BranchesValid">The branches the report has.</param>
/// <param name="BranchesCovered">The branches taken.</param>
internal sealed record Totals(int LinesValid, int LinesCovered, int LinesFull, int BranchesValid, int BranchesCovered)
{
    /// <summary>The totals of nothing yet.</summary>
    public static readonly Totals None = new(0, 0, 0, 0, 0);

    /// <summary>The totals of one line: it is valid, run when its hits are above zero, and full when run with all its branches taken.</summary>
    /// <param name="line">The line.</param>
    /// <returns>The line's totals.</returns>
    public static Totals OfLine(Line line) => new(
        1,
        line.Hits > 0 ? 1 : 0,
        line.Hits > 0 && line.Taken == line.Branches ? 1 : 0,
        line.Branches,
        line.Taken);

    /// <summary>These totals added to another's.</summary>
    /// <param name="other">The other totals.</param>
    /// <returns>The sum.</returns>
    public Totals Plus(Totals other) => new(
        LinesValid + other.LinesValid,
        LinesCovered + other.LinesCovered,
        LinesFull + other.LinesFull,
        BranchesValid + other.BranchesValid,
        BranchesCovered + other.BranchesCovered);
}
