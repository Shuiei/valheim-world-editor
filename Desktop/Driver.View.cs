using System.Text.Json;

namespace TerrainEditor.Desktop;

/// <summary>
/// The view's commands: the look switches, the camera, the frame rates and the models' boxes.
/// </summary>
public static partial class Driver
{
    /// <summary>The 'look' command (see the class summary).</summary>
    /// <param name="call">The command as given.</param>
    private static Task<string> CmdLook(Call call)
    {
        MainWindow w = call.W;
        string[] args = call.Args;
        switch (args[0])
        {
            case "game": w.GameLookBox.IsChecked = args[1] == "on"; break;
            case "seethrough": w.SeeThroughBox.IsChecked = args[1] == "on"; break;
            case "res": w.ResolutionBox.SelectedIndex = Array.IndexOf(Resolutions, args[1]); break;
            default: throw new InvalidOperationException("unknown look " + args[0]);
        }
        return Task.FromResult(State(w));
    }

    /// <summary>The 'camera' command (see the class summary).</summary>
    /// <param name="call">The command as given.</param>
    private static Task<string> CmdCamera(Call call)
    {
        MainWindow w = call.W;
        string[] args = call.Args;
        w.View.Orbit(call.F(0), call.F(1), call.F(2), call.F(3), call.F(4), args.Length > 5 ? call.F(5) : null);
        return Task.FromResult(State(w));
    }

    /// <summary>The 'bench' command (see the class summary).</summary>
    /// <param name="call">The command as given.</param>
    private static async Task<string> CmdBench(Call call)
    {
        MainWindow w = call.W;
        string result = await w.View.Benchmark(call.F(0)).WaitAsync(TimeSpan.FromSeconds(call.F(0) + 60));
        return JsonSerializer.Serialize(new { bench = result });
    }

    /// <summary>The 'bounds' command (see the class summary).</summary>
    /// <param name="call">The command as given.</param>
    private static Task<string> CmdBounds(Call call)
    {
        MainWindow w = call.W;
        string[] a = call.A;
        // bounds <prefab>...: each prefab's model box in its own frame (its parts' meshes, the root's scale),
        // for placing things by their feet and size; null for a prefab without a model.
        var models = w.View.Models ?? throw new InvalidOperationException("no models: open an area or the Workshop first");
        var boxes = new Dictionary<string, float[]?>();
        foreach (string name in a.Skip(1).SelectMany(x => x.Split(' ', StringSplitOptions.RemoveEmptyEntries)))
        {
            var model = models.LoadModel(name);
            var lo = new System.Numerics.Vector3(float.MaxValue);
            var hi = new System.Numerics.Vector3(float.MinValue);
            bool any = false;
            foreach (var part in model?.Parts ?? [])
            {
                if (models.LoadMesh(part.Mesh) is { } md)
                {
                    var (bmin, bmax) = Picking.Transform(md.Bounds.Min, md.Bounds.Max, part.Matrix);
                    lo = System.Numerics.Vector3.Min(lo, bmin);
                    hi = System.Numerics.Vector3.Max(hi, bmax);
                    any = true;
                }
            }
            var sc = model?.RootScale ?? System.Numerics.Vector3.One;
            boxes[name] = any ? [lo.X * sc.X, lo.Y * sc.Y, lo.Z * sc.Z, hi.X * sc.X, hi.Y * sc.Y, hi.Z * sc.Z] : null;
        }
        return Task.FromResult(JsonSerializer.Serialize(boxes));
    }
}
