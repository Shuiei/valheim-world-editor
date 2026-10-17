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

var files = CoverageMerge.ReadReports(args[1..]);
var (merged, totals) = CoverageMerge.Merge(files);
merged.Save(args[0]);
Console.WriteLine($"Merged {args.Length - 1} report(s): {totals.LinesFull} of {totals.LinesValid} lines covered as Codecov counts them ({100.0 * totals.LinesFull / Math.Max(totals.LinesValid, 1):F2}%: run, every branch taken); "
    + $"{totals.LinesCovered} run at all ({100.0 * totals.LinesCovered / Math.Max(totals.LinesValid, 1):F2}%); {totals.BranchesCovered} of {totals.BranchesValid} branches taken.");
return 0;
