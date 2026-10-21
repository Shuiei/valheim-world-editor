using System.Reflection;
using System.Runtime.Loader;

namespace TerrainEditor.Desktop;

// One run of the compiled script: its callbacks bound, its entry point called, and the objects on the ground placed.
public static partial class ScriptHost
{
    /// <summary>
    /// Runs the compiled script against the snapshot on the calling thread: Run without the snapping, which needs the
    /// game's piece data (the script's process has none; the editor snaps when the changes come back).
    /// </summary>
    /// <param name="image">The compiled assembly.</param>
    /// <param name="pdb">Its debug information, or null (errors then have no line).</param>
    /// <param name="snap">The area as the script sees it.</param>
    /// <param name="cancel">Stops the script at its next Check or Mountain row.</param>
    /// <returns>What the script changed; the script's own exceptions are thrown on.</returns>
    private static Changes Execute(byte[] image, byte[]? pdb, Snapshot snap, CancellationToken cancel)
    {
        var ch = new Changes();
        var calls = new Callbacks(snap, ch, cancel);
        var alc = new AssemblyLoadContext("Script", isCollectible: true);
        try
        {
            var asm = pdb != null ? alc.LoadFromStream(new MemoryStream(image), new MemoryStream(pdb)) : alc.LoadFromStream(new MemoryStream(image));
            var host = asm.GetType("__Host") ?? throw new InvalidOperationException("the script's API is missing");
            calls.Bind(host);
            var entry = asm.EntryPoint ?? throw new InvalidOperationException("the script has no statements to run");
            try
            {
                entry.Invoke(null, entry.GetParameters().Length == 0 ? null : new object[] { Array.Empty<string>() });
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            }
            ch.NoLimit = (bool)host.GetField("NoLimit")!.GetValue(null)!;
            calls.PlaceOnGround();
            return ch;
        }
        finally
        {
            alc.Unload();
        }
    }
}
