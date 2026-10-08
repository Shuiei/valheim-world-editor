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
public sealed class EditorProcess : IDisposable
{
	private readonly Process? _p;
	private readonly string _data = Path.Combine(Path.GetTempPath(), "vwe-visual-" + Guid.NewGuid().ToString("N")[..8]);
	public string? Why { get; }
	public bool Available => _p != null;

	public EditorProcess()
	{
		if (OperatingSystem.IsLinux() && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISPLAY")) && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY")))
		{
			Why = "no display to open the editor on";
			return;
		}
		string root = Path.GetFullPath(Path.Combine(Fixtures(), "..", ".."));
		string config = AppContext.BaseDirectory.Contains($"{Path.DirectorySeparatorChar}Release{Path.DirectorySeparatorChar}") ? "Release" : "Debug";
		string app = Path.Combine(root, "Desktop", "bin", config, "net8.0", "ValheimWorldEditor.Desktop.dll");
		string dotnet = Path.GetFullPath(Path.Combine(System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory(), "..", "..", "..", OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet"));
		var psi = new ProcessStartInfo(dotnet) { RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
		foreach (string a in new[] { app, "--data", _data, "--driver" })
		{
			psi.ArgumentList.Add(a);
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

	public void Dispose()
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

[CollectionDefinition("Visual")]
public sealed class VisualGroup : ICollectionFixture<EditorProcess>
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

	[Fact]
	public void AStrokeIsPendingAndTheAreaStillDraws()
	{
		Open();
		editor.Send("area 0 0 3");
		var s = editor.Send("stroke raise 0 0 30");
		Assert.Equal(1, s.GetProperty("pending").GetInt32());
		ShowsSomething(Picture("after-stroke"));
	}
}
