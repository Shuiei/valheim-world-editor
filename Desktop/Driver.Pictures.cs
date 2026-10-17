
namespace TerrainEditor.Desktop;

/// <summary>
/// The commands that paint, and take pictures of the view: a brush stroke, a picture and a shot of the window.
/// </summary>
public static partial class Driver
{
    /// <summary>The 'stroke' command (see the class summary).</summary>
    /// <param name="call">The command as given.</param>
    private static Task<string> CmdStroke(Call call)
    {
        MainWindow w = call.W;
        string[] args = call.Args;
        var s = w.Session ?? throw new InvalidOperationException("no area open");
        var tool = Enum.Parse<BrushTool>(args[0], ignoreCase: true);
        float gx = call.F(1) - s.Scene.OriginX, gz = call.F(2) - s.Scene.OriginZ;
        s.BeginStroke(tool, gx, gz);
        for (int i = 0; i < (int)call.F(3); i++)
        {
            s.StrokeStep(gx, gz, 0.05f);
        }
        s.EndStroke();
        return Task.FromResult(State(w));
    }

    /// <summary>The 'picture' command (see the class summary).</summary>
    /// <param name="call">The command as given.</param>
    private static async Task<string> CmdPicture(Call call)
    {
        MainWindow w = call.W;
        string[] a = call.A;
        try
        {
            await (w.MapShown ? w.MapPage!.Map.Picture(a[1]) : w.View.Picture(a[1])).WaitAsync(PictureWait);
        }
        catch (TimeoutException) when (!w.MapShown)
        {
            // Says what the 3D view still waits on.
            throw new TimeoutException($"No picture after {PictureWait.TotalSeconds:0} s: {w.View.PictureWaiting()}.");
        }
        return State(w);
    }

    /// <summary>The 'shot' command (see the class summary).</summary>
    /// <param name="call">The command as given.</param>
    private static async Task<string> CmdShot(Call call)
    {
        MainWindow w = call.W;
        string[] a = call.A;
        // Once the view shown has its area and models in (its own picture waits for that).
        string glPicture = Path.Combine(Path.GetTempPath(), $"vwe-shot-{Guid.NewGuid():N}.png");
        try
        {
            if (w.MapShown || w.Session != null)
            {
                await (w.MapShown ? w.MapPage!.Map.Picture(glPicture) : w.View.Picture(glPicture)).WaitAsync(PictureWait);
            }
            await WindowShot.Save(w, a[1]);
        }
        finally
        {
            File.Delete(glPicture);
        }
        return State(w);
    }
}
