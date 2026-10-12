using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using TerrainEditor.Save;
using WorldEditor.Tests;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// --driver's commands run in the window under test (the visual tests send the same ones to an editor of its own, which
/// needs a display): the map's, the 3D editor's, the panels' and controls', the Workshop's, the answers' JSON and the
/// errors; and the loop reading them on its input, one answer per line.
/// </summary>
[Collection("World files")]
public class DriverTests
{
    /// <summary>
    /// A shown window and a copy of the test world; disposed, the world is left (its session released), the window
    /// closed, then the copy deleted.
    /// </summary>
    private sealed class Run : IAsyncDisposable
    {
        public MainWindow W { get; } = new(load: false) { Width = 1600, Height = 1000 };
        public string Dir { get; } = Fixtures.CopyFixture();

        public Run()
        {
            W.Show();
            W.Tell = _ => Task.CompletedTask;
            W.Ask = (_, _, _, _) => Task.FromResult(true);
        }

        /// <summary>
        /// The command's answer, as JSON.
        /// </summary>
        public async Task<JsonElement> Send(string cmd)
        {
            string answer = await Driver.Run(W, cmd);
            Dispatcher.UIThread.RunJobs();
            return JsonDocument.Parse(answer).RootElement.Clone();
        }

        public async ValueTask DisposeAsync()
        {
            await W.LeaveWorld();
            W.CloseWithoutAsking();
            Directory.Delete(Path.GetDirectoryName(Dir)!, recursive: true);
        }
    }

    /// <summary>
    /// The test world's zone holding its wood chest.
    /// </summary>
    private static (int X, int Z) ChestZone(string dir)
    {
        var at = WorldSave.Load(dir).Objects.First(o => o.Prefab == StableHash.Of("piece_chest_wood")).Position;
        return ((int)MathF.Floor((at.X + 32) / 64), (int)MathF.Floor((at.Z + 32) / 64));
    }

    [AvaloniaFact]
    public async Task TheMapsCommandsOpenAWorldLookSearchAndPick()
    {
        await using var r = new Run();
        Assert.Equal("{}", (await r.Send($"gamedata {Fixtures.Root}")).GetRawText());
        var state = await r.Send($"world {r.Dir}");
        Assert.Equal("map", state.GetProperty("page").GetString());
        Assert.Equal(0, state.GetProperty("pending").GetInt32());
        // The map looking at a spot, at a scale.
        state = await r.Send("mapview 100 -50 4");
        Assert.Equal(100, state.GetProperty("mapX").GetSingle());
        Assert.Equal(4, state.GetProperty("mapScale").GetSingle());
        // The search box found by its hint, then searched: the test world has beeches.
        await r.Send("type Beech, Wood|Wood");
        Assert.Equal("Wood", r.W.MapPage!.SearchBox.Text);
        state = await r.Send("search Beech");
        Assert.Equal("Beech", r.W.MapPage.SearchBox.Text);
        Assert.True(state.GetProperty("pins").GetInt32() > 0);
        await r.Send("hit 0");
        Assert.Equal(0, r.W.MapPage.Hits.SelectedIndex);
        // The zone filter with its boxes cleared: every zone matches.
        state = await r.Send("zones");
        Assert.False(r.W.MapPage.NoBuildBox.IsChecked);
        Assert.True(state.GetProperty("matches").GetInt32() > 0);
        await r.Send("mappick 0 0");
        Assert.Equal((0, 0), r.W.MapPage.Spot!.Value);
        // The status bar's message, then none.
        Assert.Equal("hello there", (await r.Send("message hello there")).GetProperty("message").GetString());
        Assert.Equal("", (await r.Send("message")).GetProperty("message").GetString());
        await r.Send("wait 0");
        Assert.Equal("map", (await r.Send("state")).GetProperty("page").GetString());
    }

    [AvaloniaFact]
    public async Task TheEditorsCommandsShapeTurnInspectAndSwitch()
    {
        await using var r = new Run();
        await r.Send($"world {r.Dir}");
        var (zx, zz) = ChestZone(r.Dir);
        var state = await r.Send($"area {zx} {zz} 1");
        Assert.Equal("editor", state.GetProperty("page").GetString());
        Assert.True(state.GetProperty("objects").GetInt32() > 0);
        // A raise stroke: its zone is pending.
        float x = zx * 64, z = zz * 64;
        state = await r.Send(FormattableString.Invariant($"stroke raise {x} {z} 3"));
        Assert.True(state.GetProperty("pending").GetInt32() >= 1);
        // The camera over the spot, then the mouse and the wheel there.
        state = await r.Send(FormattableString.Invariant($"camera {x} {z} 30 50 70"));
        Assert.Equal(70, state.GetProperty("distance").GetSingle(), 0.01f);
        // Headless windows draw no frame: the camera's matrix the mouse commands use is set as a frame would.
        r.W.View.SetCamera(new System.Numerics.Vector3(0, 200, 1), new System.Numerics.Vector3(0, 30, 0), 1.6f);
        await r.Send(FormattableString.Invariant($"mouse move {x} {z}"));
        await r.Send(FormattableString.Invariant($"mouse3 move {x} 5 {z}"));
        state = await r.Send(FormattableString.Invariant($"wheel {x} {z} 3"));
        Assert.True(state.GetProperty("distance").GetSingle() < 70);
        // The View panel's Look switches.
        await r.Send("look game off");
        Assert.False(r.W.GameLookBox.IsChecked);
        await r.Send("look seethrough on");
        Assert.True(r.W.SeeThroughBox.IsChecked);
        await r.Send("look res fast");
        Assert.Equal(2, r.W.ResolutionBox.SelectedIndex);
        // The panel shown, then a list's entry, a switch, and a button by their words.
        await r.Send("panel view");
        Assert.True(r.W.ViewPanelCard.IsVisible);
        await r.Send("choose Balanced");
        Assert.Equal(1, r.W.ResolutionBox.SelectedIndex);
        await r.Send("check See-through buildings|off");
        Assert.False(r.W.SeeThroughBox.IsChecked);
        await r.Send("panel none");
        Assert.False(r.W.ViewPanelCard.IsVisible);
        bool history = r.W.History.Card.IsVisible;
        await r.Send("click History");
        Assert.NotEqual(history, r.W.History.Card.IsVisible);
        // A slider beside its label: the brush's size.
        r.W.Tools.Choose(BrushTool.Raise);
        Dispatcher.UIThread.RunJobs();
        await r.Send("set Size|9");
        Assert.Equal(9, r.W.Tools.SizeSlider.Value);
        // The chest selected and inspected; Escape closes the inspector, then gives the View tool.
        state = await r.Send("inspect piece_chest_wood");
        Assert.Equal(1, state.GetProperty("selected").GetInt32());
        Assert.Equal("Select", state.GetProperty("mode").GetString());
        Assert.True(r.W.Inspector.IsOpen);
        await r.Send("key Escape");
        Assert.False(r.W.Inspector.IsOpen);
        state = await r.Send("key Escape");
        Assert.Equal("View", state.GetProperty("mode").GetString());
        // Back to the map, the edit still pending.
        state = await r.Send("map");
        Assert.Equal("map", state.GetProperty("page").GetString());
        Assert.True(state.GetProperty("pending").GetInt32() >= 1);
    }

    [AvaloniaFact]
    public async Task AWrongCommandThrowsWithWhatIsWrong()
    {
        await using var r = new Run();
        Assert.Equal("unknown command fly", (await Assert.ThrowsAsync<InvalidOperationException>(() => r.Send("fly"))).Message);
        Assert.Equal("no area open", (await Assert.ThrowsAsync<InvalidOperationException>(() => r.Send("stroke raise 0 0 1"))).Message);
        Assert.Equal("no area open", (await Assert.ThrowsAsync<InvalidOperationException>(() => r.Send("inspect piece_chest_wood"))).Message);
        Assert.Equal("unknown look sky", (await Assert.ThrowsAsync<InvalidOperationException>(() => r.Send("look sky on"))).Message);
        Assert.Equal("no button \"Nowhere\"", (await Assert.ThrowsAsync<InvalidOperationException>(() => r.Send("click Nowhere"))).Message);
        Assert.Equal("no label \"Nothing\"", (await Assert.ThrowsAsync<InvalidOperationException>(() => r.Send("set Nothing|1"))).Message);
        Assert.Equal("no text box \"nothing\"", (await Assert.ThrowsAsync<InvalidOperationException>(() => r.Send("type nothing|1"))).Message);
        Assert.Equal("no check box \"Nothing\"", (await Assert.ThrowsAsync<InvalidOperationException>(() => r.Send("check Nothing|on"))).Message);
        Assert.Equal("no list with \"Nothing\"", (await Assert.ThrowsAsync<InvalidOperationException>(() => r.Send("choose Nothing"))).Message);
        // An area without that kind in it.
        await r.Send($"world {r.Dir}");
        await r.Send("area 0 0 1");
        Assert.Equal("no piece_nothing here", (await Assert.ThrowsAsync<InvalidOperationException>(() => r.Send("inspect piece_nothing"))).Message);
    }

    [AvaloniaFact]
    public async Task TheWorkshopOpensAndTellsHowLongItTook()
    {
        using var r = new BlueprintsPanelRun();
        r.W.Ask = (_, _, _, _) => Task.FromResult(true);
        var answer = JsonDocument.Parse(await Driver.Run(r.W, "workshop")).RootElement;
        Assert.True(r.W.Tools.Workshop);
        Assert.True(answer.GetProperty("opened").GetInt64() >= 0);
        Assert.Equal(r.W.View.Scene!.Things.Count(t => !t.Gone), answer.GetProperty("objects").GetInt32());
    }

    /// <summary>
    /// The console's lines as they are written, for the driver's answers.
    /// </summary>
    private sealed class Lines : TextWriter
    {
        private readonly StringBuilder _line = new();

        public ConcurrentQueue<string> All { get; } = new();

        public override Encoding Encoding => Encoding.UTF8;

        public override void Write(char value)
        {
            lock (_line)
            {
                if (value == '\n')
                {
                    All.Enqueue(_line.ToString().TrimEnd('\r'));
                    _line.Clear();
                }
                else
                {
                    _line.Append(value);
                }
            }
        }
    }

    [AvaloniaFact]
    public async Task TheLoopAnswersEachLineAndClosesTheWindowAfterQuit()
    {
        var w = new MainWindow(load: false) { Width = 1200, Height = 800 };
        w.Show();
        var input = Console.In;
        var output = Console.Out;
        var lines = new Lines();
        try
        {
            Console.SetIn(new StringReader("state\n\nfly\nquit\n"));
            Console.SetOut(lines);
            bool closed = false;
            w.Closed += (_, _) => closed = true;
            Driver.Start(w);
            await TestWindows.Until(() => closed);
            var answers = lines.All.Where(l => l.StartsWith("@@", StringComparison.Ordinal)).ToList();
            Assert.Equal("@@ ready", answers[0]);
            // The empty line is skipped: state, the error, quit.
            Assert.StartsWith("@@ ok {", answers[1]);
            Assert.Equal("start", JsonDocument.Parse(answers[1]["@@ ok ".Length..]).RootElement.GetProperty("page").GetString());
            Assert.Equal("@@ error unknown command fly", answers[2]);
            Assert.StartsWith("@@ ok {", answers[3]);
            Assert.Equal(4, answers.Count);
        }
        finally
        {
            Console.SetIn(input);
            Console.SetOut(output);
        }
    }
}
