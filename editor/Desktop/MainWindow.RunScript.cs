using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class MainWindow
{
    /// <summary>
    /// The question before a script Claude wrote runs (run_script, or the Script tool's Run pressed by Claude): its code
    /// shown, Run it or Refuse; true to run. Tests answer it.
    /// </summary>
    internal Func<string, Task<bool>> ConfirmClaudeScript { get; set; }

    /// <summary>
    /// The script of Claude's the user let run as a dry run, in the area open then: its run there follows without
    /// asking again, once. Null when none waits.
    /// </summary>
    private (EditSession Area, string Code)? _claudeScriptAllowed;

    /// <summary>
    /// Whether a script Claude wrote may run: the user is shown it and asked. Claude's leave in Settings is not enough on
    /// its own: text in a world (a sign, a tag, a name) can talk Claude into writing a harmful one, and a script has the
    /// user's rights on this computer. A dry run's leave also covers that code's next run in the same area (the pair
    /// asks once); the run uses it up, and another area or other code asks again.
    /// </summary>
    /// <param name="code">The script's C# source.</param>
    /// <param name="dryRun">Whether it only says what it would change.</param>
    internal async Task<bool> ClaudeMayRun(string code, bool dryRun = false)
    {
        if (_claudeScriptAllowed is { } allowed && ReferenceEquals(allowed.Area, Session) && allowed.Code == code)
        {
            if (!dryRun)
            {
                _claudeScriptAllowed = null;
            }
            return true;
        }
        _claudeScriptAllowed = null;
        if (!await ConfirmClaudeScript(code))
        {
            return false;
        }
        if (dryRun && Session is { } area)
        {
            _claudeScriptAllowed = (area, code);
        }
        return true;
    }

    /// <summary>
    /// A script on the open area (the Script tool's Run, Claude's run_script): compiled in the background, run in a
    /// process of its own on a snapshot of the area (ScriptHost.RunInChild: Stop and the 2-minute limit end that
    /// process whatever the script is doing), then what it changed goes in as one undo step, nothing when it fails,
    /// is stopped, runs 2 minutes, or the area changed meanwhile.
    /// </summary>
    /// <param name="code">The script's C# source.</param>
    /// <param name="label">The undo step's name.</param>
    /// <param name="progress">Told "Compiling…", then "Running…" (in the language in use).</param>
    /// <param name="apply">False: a dry run, only what it would change is said.</param>
    internal async Task<ScriptOutcome> RunScriptCode(string code, string label, Action<string>? progress = null, bool apply = true)
    {
        if (Session is not { } s)
        {
            return new(false, Said.Empty, Said.Of("script.msg.noArea"));
        }
        if (_stopScript != null)
        {
            return new(false, Said.Empty, Said.Of("script.msg.busy"));
        }
        progress?.Invoke(Lang.T("script.msg.compiling"));
        using var cancel = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        _stopScript = cancel.Cancel;
        var snap = ScriptHost.Take(s, NameOfPrefab);
        int generation = s.Generation;
        var watch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var (image, pdb, errors) = await Task.Run(() => ScriptHost.Compile(code));
            if (image == null)
            {
                return new(false, Said.Of("script.msg.mistakesBelow", string.Join("\n", errors)), Said.Of("script.msg.mistakes"));
            }
            progress?.Invoke(Lang.T("script.msg.running"));
            var ch = await ScriptHost.RunInChild(image, pdb, snap, cancel.Token);
            if (Session != s)
            {
                var gone = Session == null ? Said.Of("script.msg.closed") : Said.Of("script.msg.otherArea");
                return new(false, Printed(ch.Output, gone), gone);
            }
            // Its heights and object numbers are those of the area when it started: changed since (a
            // stroke, an undo, a save that read the world again), they would undo that or hit other objects.
            if (s.Generation != generation)
            {
                var changed = Said.Of("script.msg.areaChanged");
                return new(false, Printed(ch.Output, changed), changed);
            }
            if (ch.Empty)
            {
                var nothing = Said.Of("script.msg.nothing");
                return new(false, Printed(ch.Output, nothing), nothing);
            }
            var parts = new List<Said>();
            if (ch.Heights.Count > 0)
            {
                parts.Add(Said.Of("script.count.points", ch.Heights.Count));
            }

            if (ch.Paint.Count > 0)
            {
                parts.Add(Said.Of("script.count.painted", ch.Paint.Count));
            }

            if (ch.Add.Count > 0)
            {
                parts.Add(Said.Of("script.count.placed", ch.Add.Count));
            }

            int moves = ch.Moves.Keys.Count(i => !ch.Remove.Contains(i));
            if (moves > 0)
            {
                parts.Add(Said.Of("script.count.moved", moves));
            }

            if (ch.Remove.Count > 0)
            {
                parts.Add(Said.Of("script.count.removed", ch.Remove.Count));
            }

            var counts = Said.Join(", ", parts);
            if (!apply)
            {
                var would = Said.Of("script.msg.dryRun", counts);
                return new(false, Printed(ch.Output, would), would);
            }
            var (clamped, stood) = ScriptHost.Apply(s, ch, label);
            var what = Said.Of("script.msg.changed", counts, watch.Elapsed.TotalSeconds)
                + Clamped(clamped, ch.NoLimit) + CreatureStand.Note(stood);
            UpdateSaveBar();
            return new(true, Printed(ch.Output, what), what);
        }
        catch (OperationCanceledException)
        {
            // The user's Stop: not an error.
            Console.WriteLine("Script: stopped.");
            return new(false, Said.Of("script.msg.stoppedBelow"), Said.Of("script.msg.stopped"));
        }
        catch (Exception ex)
        {
            Log.Caught(ex, "running a script");
            return new(false, Said.Of("script.msg.errorBelow", ScriptHost.Describe(ex)), Said.Of("script.msg.error"));
        }
        finally
        {
            _stopScript = null;
        }
    }

    /// <summary>
    /// The sentence on the points a limit stopped (empty for none): the game's ±8 m, or with No limit on, its own ±400 m.
    /// </summary>
    /// <param name="clamped">How many points a limit stopped.</param>
    /// <param name="noLimit">Whether the script's Ground.NoLimit was on.</param>
    internal static Said Clamped(int clamped, bool noLimit) =>
        clamped == 0 ? Said.Empty : noLimit ? Said.Of("script.msg.clampedLift", clamped) : Said.Of("script.msg.clamped", clamped);
}
