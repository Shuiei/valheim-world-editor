using System.Reflection;
using Avalonia.Controls;
using Xunit.v3;

[assembly: TerrainEditor.Desktop.Tests.CloseWindowsAfterEachTest]

namespace TerrainEditor.Desktop.Tests;

// Every window a test opens is closed when that test ends (tests run in parallel: only its own). Avalonia keeps open windows, and with them
// each test's world, scene and models: the tests never closed theirs, so the run's memory grew to
// about 10 GB and CI (4 GB) ran out of it.
[AttributeUsage(AttributeTargets.Assembly)]
public sealed class CloseWindowsAfterEachTestAttribute : BeforeAfterTestAttribute
{
	// Open windows and the test that opened each (xUnit's id of the test running when it opened).
	private static readonly List<(Window Window, string? Test)> Open = new();
	private static int _hooked;
	private static volatile int _uiThread = -1;

	// Called once, when the tests' Avalonia app is built: notes every window as it opens and closes.
	internal static void Hook()
	{
		if (Interlocked.Exchange(ref _hooked, 1) != 0)
		{
			return;
		}
		Window.WindowOpenedEvent.AddClassHandler<Window>((w, _) =>
		{
			// Windows open on Avalonia's thread: noted here, so After can tell it without asking the
			// dispatcher (asked from another thread, it set the platform up again and failed).
			_uiThread = Environment.CurrentManagedThreadId;
			lock (Open)
			{
				Open.Add((w, Xunit.TestContext.Current.Test?.UniqueID));
			}
		});
		Window.WindowClosedEvent.AddClassHandler<Window>((w, _) => { lock (Open) { Open.RemoveAll(o => o.Window == w); } });
	}

	// Windows of tests that have ended, waiting to be closed on Avalonia's thread.
	private static readonly List<Window> Ended = new();

	public override void After(MethodInfo methodUnderTest, IXunitTest test)
	{
		lock (Open)
		{
			Ended.AddRange(Open.Where(o => o.Test == test.UniqueID).Select(o => o.Window));
			Open.RemoveAll(o => o.Test == test.UniqueID);
		}
		// Only on Avalonia's thread (the Avalonia tests end there); from any other thread the
		// dispatcher must not be touched, so the windows wait for the next test that ends on it.
		if (Environment.CurrentManagedThreadId != _uiThread)
		{
			return;
		}
		List<Window> close;
		lock (Open)
		{
			close = Ended.ToList();
			Ended.Clear();
		}
		foreach (var w in close)
		{
			// The main window asks before closing over unsaved changes: not here.
			if (w is MainWindow m)
			{
				m.CloseWithoutAsking();
			}
			else
			{
				w.Close();
			}
		}
	}
}
