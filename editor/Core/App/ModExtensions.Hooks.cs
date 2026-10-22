using ValheimWorldEditor.Sdk;

namespace TerrainEditor.App;

/// <summary>
/// The byte decoders and copy rewriters of the running extensions, and the editor host they are given.
/// </summary>
public static partial class ModExtensions
{
    /// <summary>
    /// A byte-array value as the running extensions read it (the first that reads it); null when none does.
    /// </summary>
    /// <param name="key">The data key's hash.</param>
    /// <param name="obj">The object holding it.</param>
    /// <param name="value">The value.</param>
    public static string? Decode(int key, IObjectView obj, byte[] value)
    {
        foreach (var l in _state.Hooks)
        {
            if (l.Decoders.TryGetValue(key, out var d) && Guard(l, () => d.Describe(obj, value), null) is { } text)
            {
                return text;
            }
        }
        return null;
    }
    /// <summary>The editor as extensions ask it (IEditorHost).</summary>
    public static IEditorHost Host { get; } = new EditorHost(AppSettings.Load);
    /// <summary>A world with nothing to find in it.</summary>
    public static IWorldView NoWorld { get; } = new EmptyWorld();

    /// <summary>Finds nothing.</summary>
    private sealed class EmptyWorld : IWorldView
    {
        /// <inheritdoc/>
        public bool TryFind(string idKey, int id, out IObjectView obj)
        {
            obj = null!;
            return false;
        }
    }
    /// <summary>
    /// Lets the running extensions rewrite one object of a pasted copy: each rewriter handling a key it holds.
    /// </summary>
    /// <param name="copy">The object, changed in place.</param>
    /// <param name="ids">The copy's new ids for the originals'.</param>
    public static void Rewrite(CopyView copy, IIdMap ids)
    {
        foreach (var l in _state.Hooks)
        {
            foreach (var (rewriter, keys) in l.Rewriters)
            {
                if (keys.Any(copy.Holds))
                {
                    Guard(l, () =>
                    {
                        rewriter.Rewrite(copy, ids);
                        return true;
                    }, false);
                }
            }
        }
    }

    /// <summary>
    /// Runs one hook of an extension; when it throws, the extension is turned off (until the editor starts again) and
    /// <paramref name="fallback"/> is the result.
    /// </summary>
    private static T Guard<T>(Loaded l, Func<T> hook, T fallback)
    {
        if (l.Fault != null)
        {
            return fallback;
        }
        try
        {
            return hook();
        }
        catch (Exception e)
        {
            Log.Caught(e, $"running a hook of the extension of {l.Mod} (turned off)");
            TurnOff(l, $"{e.GetType().Name}: {e.Message}");
            return fallback;
        }
    }

    /// <summary>Turns an extension off after a hook threw: its hooks are no longer used.</summary>
    private static void TurnOff(Loaded l, string why)
    {
        lock (Lock)
        {
            if (l.Fault != null)
            {
                return;
            }
            l.Fault = why;
            Console.WriteLine($"extension {l.Mod}: turned off ({why}).");
            var s = _state;
            // Every path it runs from (the same file offered twice).
            var dlls = s.Running.Where(r => r.Value == l).Select(r => r.Key).ToHashSet(StringComparer.Ordinal);
            _state = new State(
                [.. s.Offered.Select(o => dlls.Contains(o.Dll) ? o with { State = Status.Failed, Error = why } : o)],
                s.Running.Where(r => r.Value != l).ToDictionary(r => r.Key, r => r.Value, StringComparer.Ordinal));
            Interlocked.Increment(ref _generation);
        }
    }
}
