using System.Diagnostics;
using System.Numerics;
using System.Text;
using TerrainEditor.Save;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Scripts run in a process of their own (ScriptHost.Child): the request and the answer as sent, a script's prints,
/// changes and error through its process, Stop ending a call no Check can interrupt, and a process that dies.
/// </summary>
public class ScriptHostChildTests
{
    /// <summary>A script compiled without mistakes.</summary>
    private static (byte[] Image, byte[]? Pdb) Compiled(string code)
    {
        var (image, pdb, errors) = ScriptHost.Compile(code);
        Assert.True(image != null, string.Join("\n", errors));
        return (image!, pdb);
    }

    /// <summary>The snapshot of a flat 1 × 1 zone area.</summary>
    private static ScriptHost.Snapshot Snap() => ScriptHost.Take(TestScenes.Flat(1), Terrain.PrefabCatalog.NameOf);

    // Inside the tests, the script's process is the dotnet that runs them with the editor's assembly.
    [Fact]
    public void TheScriptsProcessIsTheEditor()
    {
        var (file, arguments) = ScriptHost.ChildCommand();
        Assert.True(File.Exists(file), file);
        Assert.Equal([typeof(ScriptHost).Assembly.Location, "--script-host"], arguments);
    }

    // The request (script, debug information, the area) and the answer (every kind of change, the output) are read
    // back as they were written.
    [Fact]
    public void TheRequestAndTheAnswerComeBackAsWritten()
    {
        var s = TestScenes.Flat(1, new WorldScene.Thing(5, StableHash.Of("Beech1"), new Vector3(32, 30, 32), new Vector3(0, 90, 0), 1.5f, false));
        var snap = ScriptHost.Take(s, Terrain.PrefabCatalog.NameOf);
        var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true))
        {
            ScriptHost.WriteRequest(w, [1, 2, 3], null, snap);
            ScriptHost.WriteRequest(w, [4], [5, 6], snap);
        }
        ms.Position = 0;
        using var r = new BinaryReader(ms, Encoding.UTF8);
        var (image, pdb, back) = ScriptHost.ReadRequest(r);
        Assert.Equal([1, 2, 3], image);
        Assert.Null(pdb);
        Assert.Equal((snap.W, snap.H, snap.Ox, snap.Oz, snap.Water, snap.Seed, snap.World), (back.W, back.H, back.Ox, back.Oz, back.Water, back.Seed, back.World));
        Assert.Equal(snap.Heights, back.Heights);
        Assert.Equal(snap.Original, back.Original);
        Assert.Equal(snap.Biomes, back.Biomes);
        Assert.Equal(snap.Mask, back.Mask);
        Assert.Equal(snap.Locked, back.Locked);
        Assert.Equal(snap.Things, back.Things);
        Assert.Equal(snap.Creatable.Order(), back.Creatable.Order());
        Assert.True(back.CanCreate(StableHash.Of("Beech1")));
        var (_, pdb2, _) = ScriptHost.ReadRequest(r);
        Assert.Equal([5, 6], pdb2);

        var ch = new ScriptHost.Changes { NoLimit = false };
        ch.Heights[3] = 31.5f;
        ch.Paint[4] = [1, 0, 0, 0];
        ch.Remove.Add(0);
        ch.Add.Add((7, "rock4_forest", new Vector3(1, float.NaN, 2), new Vector3(0, 45, 0), 1, true));
        ch.Moves[0] = (new Vector3(1, 2, 3), new Vector3(4, 5, 6), 2);
        ch.Output.Append("hi\nthere\n");
        var answer = new MemoryStream();
        using (var w = new BinaryWriter(answer, Encoding.UTF8, leaveOpen: true))
        {
            ScriptHost.WriteAnswer(w, ch);
        }
        answer.Position = 0;
        var read = ScriptHost.ReadAnswer(answer, 0, "");
        Assert.Equal(ch.Heights, read.Heights);
        Assert.Equal(ch.Paint.Keys, read.Paint.Keys);
        Assert.Equal(ch.Paint[4], read.Paint[4]);
        Assert.Equal(ch.Remove, read.Remove);
        Assert.Equal(ch.Add, read.Add);
        Assert.Equal(ch.Moves, read.Moves);
        Assert.Equal("hi\nthere\n", read.Output.ToString());
        Assert.False(read.NoLimit);
    }

    // An answer that is an error carries where, kind and message; a stop is a cancellation; no answer, or one cut
    // short, is a Crash naming the exit code and the error output.
    [Fact]
    public void ErrorsStopsAndNoAnswerAreToldApart()
    {
        var error = new MemoryStream();
        using (var w = new BinaryWriter(error, Encoding.UTF8, leaveOpen: true))
        {
            w.Write((byte)1);
            w.Write("line 2: ");
            w.Write("ArgumentException");
            w.Write("no such thing");
        }
        error.Position = 0;
        var e = Assert.Throws<ScriptHost.ScriptException>(() => ScriptHost.ReadAnswer(error, 0, ""));
        Assert.Equal(("line 2: ", "ArgumentException", "no such thing"), (e.Where, e.Kind, e.Message));
        Assert.Equal("line 2: ArgumentException: no such thing", ScriptHost.Describe(e));
        Assert.ThrowsAny<OperationCanceledException>(() => ScriptHost.ReadAnswer(new MemoryStream([2]), 0, ""));
        var crash = Assert.Throws<ScriptHost.ScriptException>(() => ScriptHost.ReadAnswer(new MemoryStream(), 134, "boom\n"));
        Assert.Equal("Crash", crash.Kind);
        Assert.Equal("the script's process ended without an answer (exit code 134):\nboom", crash.Message);
        var cut = Assert.Throws<ScriptHost.ScriptException>(() => ScriptHost.ReadAnswer(new MemoryStream([0, 9, 0]), 0, ""));
        Assert.Equal("the script's process ended without an answer (exit code 0)", cut.Message);
        // Something else than the editor wrote on the process's output: no answer either.
        Assert.Throws<ScriptHost.ScriptException>(() => ScriptHost.ReadAnswer(new MemoryStream([9]), 0, ""));
    }

    // A script's prints, ground changes and new pieces (snapped by the editor) come back through its process.
    [Fact]
    public async Task AScriptPrintsAndChangesTheAreaThroughItsProcess()
    {
        var (image, pdb) = Compiled("""
            Print("hello");
            Ground.Raise(10, 10, 2);
            Objects.Place("woodwall", 0, 0, snap: true);
            """);
        var ch = await ScriptHost.RunInChild(image, pdb, Snap(), TestContext.Current.CancellationToken);
        Assert.Equal("hello" + Environment.NewLine, ch.Output.ToString());
        Assert.Equal(32, Assert.Single(ch.Heights.Values), 3);
        Assert.Equal("woodwall", Assert.Single(ch.Add).Name);
    }

    // A script's own error comes back with its line and kind, as when it ran in the editor.
    [Fact]
    public async Task AScriptsErrorComesBackWithItsLine()
    {
        var (image, pdb) = Compiled("int a = 1;\nObjects.Place(\"Nope\", 0, 0);");
        var e = await Assert.ThrowsAsync<ScriptHost.ScriptException>(() => ScriptHost.RunInChild(image, pdb, Snap(), TestContext.Current.CancellationToken));
        Assert.Equal(("line 2: ", "ArgumentException"), (e.Where, e.Kind));
        Assert.StartsWith("the editor cannot make a “Nope”", e.Message);
    }

    // A call no Check can interrupt (Thread.Sleep for ten minutes) is ended with the process, within seconds.
    [Fact]
    public async Task StopEndsACallNoCheckCanInterrupt()
    {
        var (image, pdb) = Compiled("System.Threading.Thread.Sleep(600000);");
        using var cancel = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        var watch = Stopwatch.StartNew();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ScriptHost.RunInChild(image, pdb, Snap(), cancel.Token));
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(15), $"stopped after {watch.Elapsed}");
    }

    // A script that takes its process down is an error, not a wait.
    [Fact]
    public async Task AProcessThatDiesIsAnError()
    {
        var (image, pdb) = Compiled("Environment.FailFast(\"boom\");");
        var e = await Assert.ThrowsAsync<ScriptHost.ScriptException>(() => ScriptHost.RunInChild(image, pdb, Snap(), TestContext.Current.CancellationToken));
        Assert.Equal("Crash", e.Kind);
        Assert.StartsWith("the script's process ended without an answer (exit code ", e.Message);
        Assert.Contains("boom", e.Message);
    }
}
