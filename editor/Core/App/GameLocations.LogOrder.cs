// The lists' order from the game's own log: the lines the game writes as it adds each list (see GameLocations).

namespace TerrainEditor.App;

public static partial class GameLocations
{
    /// <summary>
    /// A log line of a list being added: its location and vegetation counts.
    /// </summary>
    private static readonly System.Text.RegularExpressions.Regex Added = new(@"Added (\d+) locations, (\d+) vegetations.* from main\s*$");

    /// <summary>
    /// The game writes a line for each list as it adds them ("Added 27 locations, 25 vegetations, ...
    /// from main"), at each start: the last run's lines in their order (lists with no location left out:
    /// they add none), or null when the log has none.
    /// </summary>
    internal static (int Locations, int Vegetation)[]? OrderFromLog(IEnumerable<string> lines)
    {
        List<(int, int)>? last = null, run = null;
        foreach (string line in lines)
        {
            var m = Added.Match(line);
            if (!m.Success)
            {
                // A run's lines come one after another: anything else between them ends it.
                if (run != null)
                {
                    last = run;
                    run = null;
                }
                continue;
            }
            run ??= [];
            var list = (int.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture), int.Parse(m.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture));
            // A list met again: the lines of another run, right after.
            if (run.Contains(list))
            {
                last = run;
                run = [];
            }
            if (list.Item1 > 0)
            {
                run.Add(list);
            }
        }
        last = run ?? last;
        return last is { Count: > 0 } ? last.ToArray() : null;
    }
}
