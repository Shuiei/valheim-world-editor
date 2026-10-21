using Avalonia.Threading;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// The driver's loop: reads the commands from the input and answers each on the output.
/// </summary>
public static partial class Driver
{
    /// <summary>
    /// Reads commands on a thread of its own, runs each on the window's thread and writes its answer; "@@ ready" once
    /// listening. The window closes when the input ends or after quit.
    /// </summary>
    public static void Start(MainWindow w)
    {
        var thread = new Thread(() =>
        {
            string? line;
            while ((line = Console.ReadLine()) != null)
            {
                string cmd = line.Trim();
                if (cmd.Length == 0)
                {
                    continue;
                }
                string answer;
                try
                {
                    answer = "@@ ok " + Dispatcher.UIThread.InvokeAsync(() => Run(w, cmd)).GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    Log.Caught(ex, $"running the driver's command {cmd.Split(' ')[0]}");
                    answer = "@@ error " + (ex.InnerException ?? ex).Message.Replace('\n', ' ');
                }
                Console.WriteLine(answer);
                Console.Out.Flush();
                if (cmd == "quit")
                {
                    break;
                }
            }
            Dispatcher.UIThread.Post(w.CloseWithoutAsking);
        })
        {
            IsBackground = true,
            Name = "Driver"
        };
        thread.Start();
        Console.WriteLine("@@ ready");
        Console.Out.Flush();
    }
}
