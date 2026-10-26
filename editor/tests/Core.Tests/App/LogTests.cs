using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The log file the console is copied into: each line with its time, whatever way the text was written. It takes the
/// console and the data folder, so it runs alone.
/// </summary>
[Collection("DataDir")]
public sealed class LogTests
{
    [Fact]
    public void TextWrittenAnyWayMakesWholeTimedLines()
    {
        string text = LogTextOf(null, () =>
        {
            // Windows' line end, by WriteLine(); characters from an array; a span.
            Console.Out.NewLine = "\r\n";
            Console.Out.Write("abc".ToCharArray(), 0, 3);
            Console.Out.WriteLine();
            Console.Out.Write("def".AsSpan());
            Console.Out.WriteLine();
        });
        // (The header's lines are written as the system ends lines; the console's end with \n alone.)
        string[] lines = text.Split('\n');
        Assert.Matches(@"^\d\d:\d\d:\d\d abc$", lines[^3]);
        Assert.Matches(@"^\d\d:\d\d:\d\d def$", lines[^2]);
        Assert.Equal("", lines[^1]);
    }

    // A console that cannot be written is said once in the log, and the log still gets every text.
    [Fact]
    public void AConsoleThatFailsIsSaidOnceAndTheLogGetsEverything()
    {
        string text = LogTextOf(new Gone(), () =>
        {
            Console.Out.Write("abc");
            Console.Out.Write("def");
        });
        Assert.Single(text.Split('\n'), l => l.EndsWith("The console could not be written (System.IO.IOException: gone); the log file still gets everything.", StringComparison.Ordinal));
        Assert.Contains("abcdef", text);
    }

    // A log that cannot be started is said on the error output, without its path, and the editor goes on without it.
    [Fact]
    public void ALogThatCannotStartIsSaidAndTheEditorGoesOn()
    {
        var (output, error, data) = (Console.Out, Console.Error, AppSettings.DataDirOverride);
        // A file where the data folder should be: the folder cannot be made.
        string file = Path.Combine(Path.GetTempPath(), "vwe-log-" + Guid.NewGuid().ToString("N")[..8]);
        File.WriteAllText(file, "");
        using var said = new StringWriter();
        try
        {
            AppSettings.DataDirOverride = file;
            Console.SetError(said);
            Log.Start("test");
        }
        finally
        {
            Console.SetOut(output);
            Console.SetError(error);
            AppSettings.DataDirOverride = data;
            File.Delete(file);
        }
        Assert.StartsWith("The log could not be started: System.IO.IOException: ", said.ToString());
        Assert.DoesNotContain(file, said.ToString());
    }

    // A caught error's line says where (file, line and member), what was being done, its type and message, then the
    // stack, whose source files keep only their name.
    [Fact]
    public void ACaughtErrorSaysWhereWhatAndWhy()
    {
        using var log = Log.Listen();
        try
        {
            throw new InvalidOperationException("broken");
        }
        catch (InvalidOperationException e)
        {
            Log.Caught(e, "testing");
        }
        string line = Assert.Single(log.Lines);
        Assert.Matches(@"^Caught at LogTests\.cs:\d+ \(ACaughtErrorSaysWhereWhatAndWhy\) while testing: System\.InvalidOperationException: broken\r?\n", line);
        Assert.Contains("at WorldEditor.Tests.LogTests.ACaughtErrorSaysWhereWhatAndWhy()", line);
        Assert.Matches(@"\) in LogTests\.cs:line \d+", line);
    }

    // A brief line: one line, the inner errors' types and messages, no stack.
    [Fact]
    public void ABriefLineHasTheInnerErrorsButNoStack()
    {
        using var log = Log.Listen();
        Log.CaughtBrief(new IOException("outer\nsecond line", new FormatException("inner")), "testing");
        string line = Assert.Single(log.Lines);
        Assert.EndsWith("while testing: System.IO.IOException: outer second line ---> System.FormatException: inner", line);
        Assert.DoesNotContain('\n', line);
    }

    // Paths (quoted, bare, under a home folder or not), web addresses, user@host, IP addresses but the local one and
    // host names before a port are taken out; versions and words with a slash are not paths. (Names of their own: other
    // tests give theirs to Log.Private, which is for the whole run, so the home folder is one of its own too, at the
    // drive's root and not under the temporary folder: the real user name may be given as private by a tunnel test, and
    // on Windows the temporary folder is under the real profile.)
    [Fact]
    public void PersonalDataIsTakenOut()
    {
        string id = Guid.NewGuid().ToString("N")[..8];
        string home = Path.Combine(Path.GetPathRoot(Path.GetTempPath())!, "vwe-home-" + id);
        string text = $"Could not find file '{Path.Combine(home, "My Worlds", "a.db")}'. Also {Path.Combine(home, "x")}, /srv/games/w.fwl and " +
            $@"C:\Games\w.db; see https://w{id}.example/a?q{id}=1 or u{id}@h{id}.example; from 203.0.113.7 and [2001:db8::1], not " +
            $"127.0.0.1 or [::1]; Connection refused (h{id}.example:22), (localhost:5182); Version=1.0.0.0, and/or 1/2.";

        Assert.Equal("Could not find file '<path>'. Also <path>, <path> and <path>; see <url> or <user@host>; from <ip> and [<ip>], " +
            "not 127.0.0.1 or [::1]; Connection refused (<host>:22), (localhost:5182); Version=1.0.0.0, and/or 1/2.", Log.Scrub(text));
    }

    // A value given as private never reaches the log; ones too short to be kept out are left. A secret is kept out
    // whatever its length, a short one where it stands alone (inside a word it would take letters out of every line).
    [Fact]
    public void PrivateValuesAndSecretsAreTakenOut()
    {
        string host = "host-" + Guid.NewGuid().ToString("N")[..8];
        Log.Private(host, "ab", null);
        Log.Secret("\u00a7\u00b6", "", null);

        Assert.Equal("refused by <private> for ab: '<private>', a\u00a7\u00b6b", Log.Scrub($"refused by {host} for ab: '\u00a7\u00b6', a\u00a7\u00b6b"));
        // A short one in a path goes with the path, not before it (which cut the path in two).
        Assert.Equal("see <path>.", Log.Scrub("see /srv/\u00a7\u00b6/w.fwl."));
    }

    // This computer's own address is not kept out, even when a tunnel or saved server gives it as private: other
    // tests' tunnels to 127.0.0.1 then no longer hide it in every later line.
    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("127.1.2.3")]
    [InlineData("localhost")]
    [InlineData("LocalHost")]
    [InlineData("::1")]
    [InlineData("[::1]")]
    public void ThisComputersAddressIsNotPrivate(string address)
    {
        Log.Private(address);

        Assert.Equal($"tunnel to {address} refused", Log.Scrub($"tunnel to {address} refused"));
    }

    // The home folder is written ~, but not as the start of another folder's name.
    [Fact]
    public void TheHomeFolderIsWrittenTilde()
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile).TrimEnd('/', '\\');
        if (home.Length < 2)
        {
            return;
        }

        Assert.Equal(Path.Combine("~", ".local", "share"), Log.WithoutHome(Path.Combine(home, ".local", "share")));
        Assert.Equal(home + "2", Log.WithoutHome(home + "2"));
    }

    /// <summary>
    /// The log's text: the log started in a new data folder (after the console is set to <paramref name="console"/>, when
    /// one is given) with <paramref name="write"/> run between its start and its reading. The console and the data
    /// folder are put back, and the folder deleted, after.
    /// </summary>
    private static string LogTextOf(TextWriter? console, Action write)
    {
        var (output, error, data) = (Console.Out, Console.Error, AppSettings.DataDirOverride);
        string dir = Path.Combine(Path.GetTempPath(), "vwe-log-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            AppSettings.DataDirOverride = dir;
            if (console != null)
            {
                Console.SetOut(console);
            }
            Log.Start("test");
            write();
            return Log.Read();
        }
        finally
        {
            Console.SetOut(output);
            Console.SetError(error);
            AppSettings.DataDirOverride = data;
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    /// <summary>A console that cannot be written.</summary>
    private sealed class Gone : TextWriter
    {
        /// <inheritdoc/>
        public override System.Text.Encoding Encoding => System.Text.Encoding.UTF8;

        /// <inheritdoc/>
        public override void Write(char value) => throw new IOException("gone");

        /// <inheritdoc/>
        public override void Write(string? value) => throw new IOException("gone");
    }
}
