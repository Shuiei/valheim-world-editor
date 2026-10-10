using System.Diagnostics;
using System.Text.Json;
using SkiaSharp;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

// The real editor, drawing with OpenGL: one editor process for the whole group (started once, driven
// with --driver commands), each test opening a fresh copy of the test world. Needs a display (and a
// graphics card or Mesa's software renderer); without one the tests skip. Its settings and memory go
// to a test folder (--data), never the user's. Run only these: --filter Category=Visual; skip them:
// --filter Category!=Visual.
public class EditorProcess : IDisposable
{
	private readonly Process? _p;
	private readonly string _data = Path.Combine(Path.GetTempPath(), "vwe-visual-" + Guid.NewGuid().ToString("N")[..8]);
	public string? Why { get; }
	public bool Available => _p != null;
	// The editor's data folder (--data): its settings, log...
	public string Data => _data;

	public EditorProcess()
		: this(Array.Empty<string>())
	{
	}

	// Started with more options (the world to open at once, for example), and environment variables.
	protected EditorProcess(string[] extra, IReadOnlyDictionary<string, string>? env = null)
	{
		if (OperatingSystem.IsLinux() && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISPLAY")) && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY")))
		{
			Why = "no display to open the editor on";
			return;
		}
		string root = Path.GetFullPath(Path.Combine(Fixtures(), "..", ".."));
		string config = AppContext.BaseDirectory.Contains($"{Path.DirectorySeparatorChar}Release{Path.DirectorySeparatorChar}") ? "Release" : "Debug";
		string app = Path.Combine(root, "Desktop", "bin", config, "net10.0", "ValheimWorldEditor.dll");
		string dotnet = Path.GetFullPath(Path.Combine(System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory(), "..", "..", "..", OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet"));
		var psi = new ProcessStartInfo(dotnet) { RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
		foreach (string a in new[] { app, "--data", _data, "--driver" }.Concat(extra))
		{
			psi.ArgumentList.Add(a);
		}
		foreach (var (k, v) in env ?? new Dictionary<string, string>())
		{
			psi.Environment[k] = v;
		}
		_p = Process.Start(psi)!;
		_p.ErrorDataReceived += (_, _) => { };
		_p.BeginErrorReadLine();
		string? first = Next(TimeSpan.FromSeconds(60));
		if (first != "@@ ready")
		{
			Why = "the editor did not start: " + first;
			_p.Kill();
			_p = null;
		}
	}

	internal static string Fixtures()
	{
		for (string? d = AppContext.BaseDirectory; d != null; d = Path.GetDirectoryName(d))
		{
			if (Directory.Exists(Path.Combine(d, "tests", "fixtures")))
			{
				return Path.Combine(d, "tests", "fixtures");
			}
		}
		throw new DirectoryNotFoundException("tests/fixtures");
	}

	// The next answer line ("@@ ..."), skipping the editor's other output.
	private string? Next(TimeSpan timeout)
	{
		var watch = Stopwatch.StartNew();
		while (watch.Elapsed < timeout)
		{
			var read = _p!.StandardOutput.ReadLineAsync();
			if (!read.Wait(timeout - watch.Elapsed))
			{
				return null;
			}
			if (read.Result == null)
			{
				return null;
			}
			if (read.Result.StartsWith("@@ "))
			{
				return read.Result;
			}
		}
		return null;
	}

	// Sends a command; its answer's JSON (an error fails the test).
	public JsonElement Send(string command)
	{
		lock (this)
		{
			_p!.StandardInput.WriteLine(command);
			_p.StandardInput.Flush();
			string? answer = Next(TimeSpan.FromSeconds(120));
			Assert.NotNull(answer);
			Assert.True(answer!.StartsWith("@@ ok "), $"{command}: {answer}");
			return JsonDocument.Parse(answer["@@ ok ".Length..]).RootElement.Clone();
		}
	}

	public virtual void Dispose()
	{
		if (_p != null)
		{
			try
			{
				_p.StandardInput.WriteLine("quit");
				_p.StandardInput.Flush();
				if (!_p.WaitForExit(15000))
				{
					_p.Kill();
				}
			}
			catch (Exception)
			{
			}
		}
		try
		{
			Directory.Delete(_data, true);
		}
		catch (Exception)
		{
		}
	}
}

// The editor started with --world and --zone: straight into the 3D view of a copy of the test world.
public sealed class DirectEditorProcess() : EditorProcess(Start())
{
	private static string? _copy;

	private static string[] Start()
	{
		_copy = Path.Combine(Path.GetTempPath(), "vwe-visual-direct-" + Guid.NewGuid().ToString("N")[..8]);
		string world = Path.Combine(_copy, "CITest");
		Directory.CreateDirectory(world);
		foreach (string f in Directory.GetFiles(Path.Combine(Fixtures(), "CITest")))
		{
			File.Copy(f, Path.Combine(world, Path.GetFileName(f)));
		}
		return new[] { "--world", world, "--zone", "0,0", "--size", "2" };
	}

	public override void Dispose()
	{
		base.Dispose();
		try
		{
			Directory.Delete(_copy!, true);
		}
		catch (Exception)
		{
		}
	}
}

// Started straight in the 3D editor with a graphics-card budget of 1 byte: every area opened lets the
// models go and reads them again.
public sealed class TinyBudgetEditorProcess() : EditorProcess(Start(), new Dictionary<string, string> { ["VWE_GPU_BUDGET"] = "1" })
{
	private static string? _copy;

	private static string[] Start()
	{
		_copy = Path.Combine(Path.GetTempPath(), "vwe-visual-budget-" + Guid.NewGuid().ToString("N")[..8]);
		string world = Path.Combine(_copy, "CITest");
		Directory.CreateDirectory(world);
		foreach (string f in Directory.GetFiles(Path.Combine(Fixtures(), "CITest")))
		{
			File.Copy(f, Path.Combine(world, Path.GetFileName(f)));
		}
		return new[] { "--world", world, "--zone", "0,0", "--size", "2" };
	}

	public override void Dispose()
	{
		base.Dispose();
		try
		{
			Directory.Delete(_copy!, true);
		}
		catch (Exception)
		{
		}
	}
}

// The editor started with a world folder: that world's map.
public sealed class FolderEditorProcess() : EditorProcess(Start())
{
	private static string? _copy;

	private static string[] Start()
	{
		_copy = Path.Combine(Path.GetTempPath(), "vwe-visual-folder-" + Guid.NewGuid().ToString("N")[..8]);
		string world = Path.Combine(_copy, "CITest");
		Directory.CreateDirectory(world);
		foreach (string f in Directory.GetFiles(Path.Combine(Fixtures(), "CITest")))
		{
			File.Copy(f, Path.Combine(world, Path.GetFileName(f)));
		}
		return new[] { world };
	}

	public override void Dispose()
	{
		base.Dispose();
		try
		{
			Directory.Delete(_copy!, true);
		}
		catch (Exception)
		{
		}
	}
}

// The editor started with --live and --token: the stand-in game's map.
public sealed class LiveEditorProcess() : EditorProcess(Start())
{
	private static FakeGame? _game;
	public FakeGame Game => _game!;

	private static string[] Start()
	{
		_game = new FakeGame();
		return new[] { "--live", _game.Url, "--token", _game.Token };
	}

	public override void Dispose()
	{
		base.Dispose();
		_game?.Dispose();
	}
}

[CollectionDefinition("Visual folder")]
public sealed class FolderVisualGroup : ICollectionFixture<FolderEditorProcess>
{
}

[CollectionDefinition("Visual live")]
public sealed class LiveVisualGroup : ICollectionFixture<LiveEditorProcess>
{
}

[CollectionDefinition("Visual")]
public sealed class VisualGroup : ICollectionFixture<EditorProcess>
{
}

[CollectionDefinition("Visual direct")]
public sealed class DirectVisualGroup : ICollectionFixture<DirectEditorProcess>
{
}

[Collection("Visual")]
[Trait("Category", "Visual")]
public sealed class VisualTests(EditorProcess editor) : IDisposable
{
	private readonly string _dir = Path.Combine(Path.GetTempPath(), "vwe-visual-world-" + Guid.NewGuid().ToString("N")[..8]);

	public void Dispose()
	{
		try
		{
			Directory.Delete(_dir, true);
		}
		catch (Exception)
		{
		}
	}

	// A fresh copy of the test world, opened (its map shown).
	private JsonElement Open()
	{
		Assert.SkipUnless(editor.Available, editor.Why ?? "");
		string world = Path.Combine(_dir, "CITest");
		Directory.CreateDirectory(world);
		foreach (string f in Directory.GetFiles(Path.Combine(EditorProcess.Fixtures(), "CITest")))
		{
			File.Copy(f, Path.Combine(world, Path.GetFileName(f)));
		}
		return editor.Send($"world {world}");
	}

	private sealed record Look(double Mean, double Spread, int Colours, double Dark);

	// How much a picture shows: average brightness, its spread, distinct colours, share of near-black.
	private Look Picture(string name)
	{
		string path = Path.Combine(_dir, name + ".png");
		editor.Send($"picture {path}");
		return Picture(name, path);
	}

	private static Look Picture(string name, string path)
	{
		using var bmp = SKBitmap.Decode(path);
		Assert.NotNull(bmp);
		var colours = new HashSet<uint>();
		double sum = 0, sq = 0;
		int dark = 0, n = 0;
		for (int y = 0; y < bmp.Height; y += 3)
		{
			for (int x = 0; x < bmp.Width; x += 3)
			{
				var c = bmp.GetPixel(x, y);
				double l = 0.2126 * c.Red + 0.7152 * c.Green + 0.0722 * c.Blue;
				sum += l;
				sq += l * l;
				dark += l < 12 ? 1 : 0;
				colours.Add((uint)(c.Red >> 3 << 10 | c.Green >> 3 << 5 | c.Blue >> 3));
				n++;
			}
		}
		double mean = sum / n;
		return new Look(mean, Math.Sqrt(sq / n - mean * mean), colours.Count, dark / (double)n);
	}

	private static void ShowsSomething(Look l)
	{
		Assert.True(l.Spread > 12, $"the picture is flat (brightness spread {l.Spread:0.0})");
		Assert.True(l.Colours > 300, $"the picture has few colours ({l.Colours})");
	}

	// The window as the user sees it, panels and view: what the documentation's pictures are.
	private Look Shot(string name)
	{
		string path = Path.Combine(_dir, name + ".png");
		editor.Send($"shot {path}");
		Assert.True(File.Exists(path));
		return Picture(name, path);
	}

	[Fact]
	public void AWindowShotHasThePanelsAndTheView()
	{
		Open();
		var map = Shot("shot-map");
		ShowsSomething(map);
		editor.Send("area 0 0 2");
		var area = Shot("shot-area");
		ShowsSomething(area);
		// The view is in it (daylight, not the empty dark the window alone gives).
		Assert.True(area.Dark < 0.3, $"{area.Dark:P0} of the window shot is black");
	}

	[Fact]
	public void TheDocumentationsCommandsPlaceTheCameraAndUseTheControls()
	{
		Open();
		editor.Send("area 0 0 2");
		var s = editor.Send("camera 10 -5 45 40 60");
		Assert.Equal(45 * Math.PI / 180, s.GetProperty("yaw").GetDouble(), 3);
		Assert.Equal(60, s.GetProperty("distance").GetDouble(), 3);
		// A tool's panel: its list entries and buttons by their words.
		editor.Send("key D1");
		editor.Send("choose Square");
		editor.Send("click History");
		editor.Send("click History");
		Assert.Equal("editor", editor.Send("state").GetProperty("page").GetString());
		editor.Send("choose Circle");
	}

	[Fact]
	public void TheEditorStillDrawsItsModelsAfterAVisitToTheMap()
	{
		Open();
		editor.Send("area 0 0 2");
		editor.Send("camera 40 20 30 50 70");
		var before = Picture("models-before");
		Assert.Equal("map", editor.Send("map").GetProperty("page").GetString());
		editor.Send("area 0 0 2");
		editor.Send("camera 40 20 30 50 70");
		var after = Picture("models-after");
		// The models came back untextured once (the textures being read were never read again in
		// the view's new OpenGL context): trees as flat white leaf cards. Same look as before.
		Assert.True(Math.Abs(after.Mean - before.Mean) < 8, $"brightness {before.Mean:0} before, {after.Mean:0} after");
		Assert.True(Math.Abs(after.Spread - before.Spread) < 8, $"spread {before.Spread:0} before, {after.Spread:0} after");
		Assert.Equal(0, editor.Send("state").GetProperty("glErrors").GetInt32());
	}

	// Areas switched while their models are still being read: a model read for the area left was put
	// into the new one, with that area's object numbers (out of range: the app closed).
	[Fact]
	public void SwitchingAreasWhileModelsLoadKeepsDrawing()
	{
		Open();
		for (int n = 0; n < 4; n++)
		{
			editor.Send("area 0 0 3");
			editor.Send("area 0 0 1");
		}
		editor.Send("camera 0 0 30 50 70");
		var picture = Picture("switched");
		Assert.True(picture.Spread > 1, "the view is drawn");
		var state = editor.Send("state");
		Assert.Equal("editor", state.GetProperty("page").GetString());
		Assert.Equal(0, state.GetProperty("glErrors").GetInt32());
	}

	[Fact]
	public void TheMapDrawsTheWorld()
	{
		Assert.Equal("map", Open().GetProperty("page").GetString());
		var map = Picture("map");
		ShowsSomething(map);
		Assert.True(editor.Send("state").GetProperty("frames").GetInt32() > 10);
	}

	[Fact]
	public void TheEditorDrawsTheArea()
	{
		Open();
		var s = editor.Send("area 0 0 3");
		Assert.Equal("editor", s.GetProperty("page").GetString());
		Assert.True(s.GetProperty("objects").GetInt32() > 10);
		var area = Picture("area");
		ShowsSomething(area);
		// Daylight ground and water, not the dark of space.
		Assert.True(area.Dark < 0.2, $"{area.Dark:P0} of the picture is black");
	}

	[Fact]
	public void TheMapStillDrawsAfterAVisitToTheEditor()
	{
		Open();
		var before = Picture("map-before");
		editor.Send("area 0 0 3");
		Picture("area");
		Assert.Equal("map", editor.Send("map").GetProperty("page").GetString());
		var after = Picture("map-after");
		// The map came back black once (textures of the 3D view left bound): same look as before.
		ShowsSomething(after);
		Assert.True(Math.Abs(after.Mean - before.Mean) < 15, $"brightness {before.Mean:0} before, {after.Mean:0} after");
		Assert.True(Math.Abs(after.Dark - before.Dark) < 0.15, $"black {before.Dark:P0} before, {after.Dark:P0} after");
	}

	// Every picture drawn without an OpenGL error.
	private void Clean() => Assert.Equal(0, editor.Send("state").GetProperty("glErrors").GetInt32());

	private JsonElement Do(params string[] commands)
	{
		JsonElement last = default;
		foreach (var c in commands)
		{
			last = editor.Send(c);
		}
		return last;
	}

	[Fact]
	public void DrawingAZoneSelectsAndShowsTheHandles()
	{
		Open();
		Do("area 0 0 3", "key E");
		Do("mouse down -40 -40", "mouse move 40 -40", "mouse move 40 40", "mouse move -40 40");
		// The zone being drawn.
		ShowsSomething(Picture("lasso"));
		var s = Do("mouse move -40 -39", "mouse up -40 -39");
		// The selection settles on the next frames (once in a while, under load, it was not there yet).
		for (int i = 0; i < 20 && s.GetProperty("selected").GetInt32() == 0; i++)
		{
			s = editor.Send("wait 100");
		}
		Assert.True(s.GetProperty("selected").GetInt32() > 0, s.ToString());
		Assert.True(s.GetProperty("mode").GetString() == "Select", s.ToString());
		// The selection's boxes and the handles.
		ShowsSomething(Picture("selected"));
		Clean();
	}

	[Fact]
	public void PlacePreviewsAndPlacesTrees()
	{
		Open();
		Do("area 0 0 3", "key T", "mouse move 5 5");
		ShowsSomething(Picture("place-preview"));
		var s = Do("mouse down 5 5", "mouse up 5 5");
		Assert.True(s.GetProperty("added").GetInt32() > 0, s.ToString());
		ShowsSomething(Picture("placed"));
		Clean();
	}

	[Fact]
	public void TheBrushCircleFollowsTheMouseAndAStrokeChangesTheGround()
	{
		Open();
		Do("area 0 0 3", "key D1", "mouse move 0 0");
		ShowsSomething(Picture("brush"));
		// Held for a moment: the brush works frame by frame while the button is down.
		var s = Do("mouse down 0 0", "wait 150", "mouse move 2 0", "wait 150", "mouse move 4 0", "wait 150", "mouse up 4 0");
		Assert.Equal(1, s.GetProperty("pending").GetInt32());
		Clean();
	}

	[Fact]
	public void AnAreaBoxAPathAndTheTapeAreDrawn()
	{
		Open();
		Do("area 0 0 3", "key B", "mouse down -20 -20", "mouse move 0 0", "mouse move 20 20", "mouse up 20 20");
		ShowsSomething(Picture("area-box"));
		Do("key Escape", "key P", "mouse down -30 0", "mouse up -30 0", "mouse down 0 10", "mouse up 0 10", "mouse down 30 0", "mouse up 30 0");
		ShowsSomething(Picture("path"));
		Do("key Escape", "key M", "mouse down -20 5", "mouse up -20 5", "mouse down 25 -5", "mouse up 25 -5");
		ShowsSomething(Picture("tape"));
		Clean();
	}

	[Fact]
	public void ACopiedAreaIsShownWhilePasting()
	{
		Open();
		Do("area 0 0 3", "key B", "mouse down -30 -30", "mouse move 0 0", "mouse move 30 30", "mouse up 30 30", "key C ctrl", "key V ctrl", "mouse move 10 10");
		Assert.Equal("Paste", editor.Send("state").GetProperty("mode").GetString());
		ShowsSomething(Picture("paste"));
		var s = Do("mouse down 10 10", "mouse up 10 10");
		Assert.True(s.GetProperty("added").GetInt32() > 0, s.ToString());
		Clean();
	}

	[Fact]
	public void TheMouseTurnsZoomsAndSlidesTheView()
	{
		Open();
		var s0 = Do("area 0 0 3", "key Escape");
		var s1 = Do("mouse down 0 0 right", "mouse move 20 0 right", "mouse up 20 0 right");
		Assert.NotEqual(s0.GetProperty("yaw").GetSingle(), s1.GetProperty("yaw").GetSingle());
		var s2 = Do("wheel 0 0 3");
		Assert.True(s2.GetProperty("distance").GetSingle() < s1.GetProperty("distance").GetSingle(), $"{s1} → {s2}");
		var s3 = Do("mouse down 0 0 middle", "mouse move 15 15 middle", "mouse up 15 15 middle");
		Assert.NotEqual(s2.GetProperty("targetX").GetSingle(), s3.GetProperty("targetX").GetSingle());
		ShowsSomething(Picture("moved-view"));
		Clean();
	}

	[Fact]
	public void TheMapShowsBuildingsCloseUpSearchPinsAndZoneMatches()
	{
		Open();
		var s = Do("mapview 0 0 0.5");
		Assert.Equal(0.5, s.GetProperty("mapScale").GetSingle(), 3);
		// Close up: the 1 m detail (with the edits) and the buildings' outlines. Mostly one biome: fewer
		// colours than the whole map.
		var close = Picture("map-close");
		Assert.True(editor.Send("state").GetProperty("detail").GetBoolean(), "the close-up detail is not shown");
		Assert.True(close.Spread > 12 && close.Colours > 150, $"{close}");
		s = Do("search Beech", "mapview 0 0 3");
		Assert.True(s.GetProperty("pins").GetInt32() > 0, s.ToString());
		s = Do("zones");
		Assert.True(s.GetProperty("matches").GetInt32() > 0, s.ToString());
		ShowsSomething(Picture("map-marks"));
		Clean();
	}

	// An object found by the map's search, opened with Edit in 3D: still selected once the area is
	// drawn (the first frame dropped the selection made as the area opened).
	[Fact]
	public void AFoundObjectStaysSelectedInTheEditor()
	{
		Open();
		var s = Do("search Beech", "hit 0", "click Edit in 3D");
		for (int n = 0; n < 120 && s.GetProperty("page").GetString() != "editor"; n++)
		{
			s = editor.Send("wait 250");
		}
		Assert.Equal("editor", s.GetProperty("page").GetString());
		Picture("found");
		Assert.Equal(1, editor.Send("state").GetProperty("selected").GetInt32());
		Clean();
	}

	[Fact]
	public void AStrokeIsPendingAndTheAreaStillDraws()
	{
		Open();
		editor.Send("area 0 0 3");
		var s = editor.Send("stroke raise 0 0 30");
		Assert.Equal(1, s.GetProperty("pending").GetInt32());
		ShowsSomething(Picture("after-stroke"));
	}
	[Fact]
	public void TheLookSwitchesAndALowerResolutionStillDraw()
	{
		Open();
		editor.Send("area 0 0 3");
		try
		{
			var sharp = Picture("res-sharp");
			// Fewer pixels, stretched to the view: the same scene, a little softer.
			editor.Send("look res fast");
			var fast = Picture("res-fast");
			Assert.True(fast.Spread > sharp.Spread * 0.7, $"brightness spread {sharp.Spread:0.0} sharp, {fast.Spread:0.0} fast");
			Assert.True(fast.Colours > 300, $"the picture has few colours ({fast.Colours})");
			Assert.True(Math.Abs(fast.Mean - sharp.Mean) < 10, $"brightness {sharp.Mean:0} sharp, {fast.Mean:0} fast");
			Assert.True(fast.Dark < 0.2, $"{fast.Dark:P0} of the picture is black");
			editor.Send("look res balanced");
			ShowsSomething(Picture("res-balanced"));
			editor.Send("look res sharp");
			// See-through buildings, and the plain look with boxes and back.
			editor.Send("look seethrough on");
			ShowsSomething(Picture("see-through"));
			editor.Send("look seethrough off");
			int objects = editor.Send("look game off").GetProperty("objects").GetInt32();
			// Plain colours and boxes: far fewer colours than the game's textures, still not dark.
			var plain = Picture("plain");
			Assert.True(plain.Colours < sharp.Colours / 2, $"{plain.Colours} colours plain, {sharp.Colours} with the game look");
			Assert.True(plain.Colours > 10 && plain.Dark < 0.2, $"{plain.Colours} colours, {plain.Dark:P0} black");
			Assert.Equal(objects, editor.Send("look game on").GetProperty("objects").GetInt32());
			ShowsSomething(Picture("game-look"));
			Clean();
		}
		finally
		{
			// The editor is shared: back to the usual look for the other tests.
			editor.Send("look res sharp");
			editor.Send("look seethrough off");
			editor.Send("look game on");
		}
	}
}

// Started straight in the 3D editor (--world, --zone): the area is open, and the benchmark runs.
[Collection("Visual direct")]
[Trait("Category", "Visual")]
public sealed class DirectVisualTests(DirectEditorProcess editor)
{
	[Fact]
	public void TheWorldOpensStraightInTheEditorAndTheBenchmarkRuns()
	{
		Assert.SkipUnless(editor.Available, editor.Why ?? "");
		// The area loads after the window opens: wait for it.
		JsonElement s = editor.Send("state");
		for (int i = 0; i < 200 && s.GetProperty("page").GetString() != "editor"; i++)
		{
			Thread.Sleep(100);
			s = editor.Send("state");
		}
		Assert.Equal("editor", s.GetProperty("page").GetString());
		Assert.True(s.GetProperty("objects").GetInt32() > 0);
		string bench = editor.Send("bench 1").GetProperty("bench").GetString()!;
		Assert.Matches(@"^\d+ fps over \d+\.\d s, work .* ms, gaps median \d+ ms, 95% \d+ ms, max \d+ ms, view \d+×\d+ px$", bench);
		Assert.Equal(0, editor.Send("state").GetProperty("glErrors").GetInt32());
		// What it said is in its log, in the tests' data folder.
		string log = File.ReadAllText(Path.Combine(editor.Data, "ValheimWorldEditor.log"));
		Assert.StartsWith($"Valheim World Editor {BuildInfo.Version}, ", log);
		Assert.Contains("window open", log);
	}

	internal static JsonElement WaitForPage(EditorProcess editor, string page)
	{
		JsonElement s = editor.Send("state");
		for (int i = 0; i < 300 && s.GetProperty("page").GetString() != page; i++)
		{
			Thread.Sleep(100);
			s = editor.Send("state");
		}
		Assert.Equal(page, s.GetProperty("page").GetString());
		return s;
	}
}

// Started with a world folder: its map opens.
[Collection("Visual folder")]
[Trait("Category", "Visual")]
public sealed class FolderVisualTests(FolderEditorProcess editor)
{
	[Fact]
	public void AWorldFolderOnTheCommandLineOpensItsMap()
	{
		Assert.SkipUnless(editor.Available, editor.Why ?? "");
		DirectVisualTests.WaitForPage(editor, "map");
	}
}

// Started with --live and --token: the game's world opens on the map.
[Collection("Visual live")]
[Trait("Category", "Visual")]
public sealed class LiveVisualTests(LiveEditorProcess editor)
{
	[Fact]
	public void ALiveGameOnTheCommandLineOpensItsMap()
	{
		Assert.SkipUnless(editor.Available, editor.Why ?? "");
		DirectVisualTests.WaitForPage(editor, "map");
	}
}

[CollectionDefinition("Visual budget")]
public sealed class VisualBudgetCollection : ICollectionFixture<TinyBudgetEditorProcess>
{
}

// Past the graphics-card budget, opening another area lets the models go: they are read again, and
// the area still draws them (with their textures).
[Collection("Visual budget")]
[Trait("Category", "Visual")]
public sealed class BudgetVisualTests(TinyBudgetEditorProcess editor)
{
	[Fact]
	public void ModelsLetGoAreReadAgain()
	{
		Assert.SkipUnless(editor.Available, editor.Why ?? "");
		DirectVisualTests.WaitForPage(editor, "editor");
		string dir = Path.Combine(Path.GetTempPath(), "vwe-visual-budget-pics-" + Guid.NewGuid().ToString("N")[..8]);
		Directory.CreateDirectory(dir);
		try
		{
			editor.Send("camera 40 20 30 50 70");
			editor.Send($"picture {Path.Combine(dir, "a.png")}");
			for (int n = 0; n < 2; n++)
			{
				editor.Send("area 0 0 2");
			}
			editor.Send("camera 40 20 30 50 70");
			editor.Send($"picture {Path.Combine(dir, "b.png")}");
			Assert.Equal(0, editor.Send("state").GetProperty("glErrors").GetInt32());
			string log = File.ReadAllText(Path.Combine(editor.Data, "ValheimWorldEditor.log"));
			Assert.Contains("Models let go from the graphics card", log);
			using var a = SkiaSharp.SKBitmap.Decode(Path.Combine(dir, "a.png"));
			using var b = SkiaSharp.SKBitmap.Decode(Path.Combine(dir, "b.png"));
			Assert.Equal(Mean(a), Mean(b), 0);
		}
		finally
		{
			Directory.Delete(dir, true);
		}
	}

	private static double Mean(SkiaSharp.SKBitmap bmp)
	{
		double sum = 0;
		int n = 0;
		for (int y = 0; y < bmp.Height; y += 4)
		{
			for (int x = 0; x < bmp.Width; x += 4)
			{
				var c = bmp.GetPixel(x, y);
				sum += 0.2126 * c.Red + 0.7152 * c.Green + 0.0722 * c.Blue;
				n++;
			}
		}
		return sum / n / 8;
	}
}
