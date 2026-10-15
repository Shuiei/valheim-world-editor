using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using TerrainEditor.Save;
using ValheimWorldEditor.Sdk;

namespace TerrainEditor.App;

/// <summary>
/// Mods' editor extensions (ValheimWorldEditor.Sdk; docs/modding.md#extensions): a DLL a mod offers in its
/// worldeditor.json (ModManifests.Extensions), which runs in the editor only once the user allows it in Settings, and
/// then only that file (AppSettings.Extensions keeps its SHA-256: a changed DLL is other code and asks again; the
/// bytes hashed are the bytes loaded). Each is one DLL, loaded in a context of its own that gives it the editor's SDK
/// and the framework and nothing from its folder, and registers hooks: byte decoders for the inspector,
/// rewriters of pasted copies, shapes to draw, ground cuts, live listeners (LiveConnected). Read as a world or the Workshop opens, after the manifests. A hook that
/// throws turns its extension off until the editor starts again; the editor never fails because of one.
/// </summary>
public static class ModExtensions
{
    /// <summary>What became of an offered extension.</summary>
    public enum Status
    {
        /// <summary>Not allowed (the user has not turned it on, or the file changed since).</summary>
        Off,

        /// <summary>Allowed, loaded and its hooks in use.</summary>
        Running,

        /// <summary>Allowed but it could not run, or a hook of it threw (see <see cref="Extension.Error"/>).</summary>
        Failed,
    }

    /// <summary>An extension a mod offers.</summary>
    /// <param name="Dll">Its DLL's full path.</param>
    /// <param name="Mod">The mod offering it (its folder under plugins).</param>
    /// <param name="Sha256">The file's SHA-256, lower-case hex ("" when it cannot be read).</param>
    /// <param name="State">What became of it.</param>
    /// <param name="Error">Why it does not run, for <see cref="Status.Failed"/>.</param>
    public sealed record Extension(string Dll, string Mod, string Sha256, Status State, string? Error);

    /// <summary>One loaded extension: what it registered.</summary>
    private sealed class Loaded(string mod)
    {
        /// <summary>The mod offering it, for messages.</summary>
        public string Mod { get; } = mod;

        /// <summary>Its byte decoders, by data key hash.</summary>
        public Dictionary<int, IDataDecoder> Decoders { get; } = [];

        /// <summary>Its copy rewriters, with the data key hashes each handles.</summary>
        public List<(ICopyRewriter Rewriter, HashSet<int> Keys)> Rewriters { get; } = [];

        /// <summary>Its shape providers, by prefab hash.</summary>
        public Dictionary<int, List<IShapeProvider>> Shapes { get; } = [];

        /// <summary>Its ground cutters, by prefab hash.</summary>
        public Dictionary<int, List<IGroundCutter>> Cutters { get; } = [];

        /// <summary>Its live listeners.</summary>
        public List<ILiveListener> LiveListeners { get; } = [];

        /// <summary>Why it was turned off (a hook threw); null while it runs.</summary>
        public string? Fault { get; set; }
    }

    /// <summary>The extensions in use and what is known of the offered ones, swapped in whole.</summary>
    /// <param name="Offered">Every offered extension.</param>
    /// <param name="Running">The loaded ones whose hooks are in use, by DLL path (one file at two paths: the same one at
    /// both).</param>
    private sealed record State(IReadOnlyList<Extension> Offered, IReadOnlyDictionary<string, Loaded> Running)
    {
        /// <summary>Each loaded extension in use once, however many paths it runs from: whose hooks are called.</summary>
        public IReadOnlyList<Loaded> Hooks { get; } = [.. Running.Values.Distinct()];

        /// <summary>Every data key a running rewriter handles.</summary>
        public HashSet<int> RewrittenKeys { get; } = [.. Running.Values.SelectMany(l => l.Rewriters).SelectMany(r => r.Keys)];
    }

    /// <summary>Extensions loaded in this run, by file hash: an allowed file is loaded once (a .NET context stays).</summary>
    private static readonly Dictionary<string, (Loaded? Loaded, string? Error)> LoadedFiles = [];

    /// <summary>Serialises refreshing and loading.</summary>
    private static readonly Lock Lock = new();

    /// <summary>What is in use.</summary>
    private static volatile State _state = new([], new Dictionary<string, Loaded>());

    /// <summary>Counts each time what is in use changes, for what is worked out from it (drawn shapes) to be redone.</summary>
    public static int Generation => _generation;

    /// <summary>Backs <see cref="Generation"/>.</summary>
    private static volatile int _generation;

    /// <summary>The extensions the mods offer, and what became of each.</summary>
    public static IReadOnlyList<Extension> Offered => _state.Offered;

    /// <summary>
    /// Every data key a running extension rewrites in pasted copies (a manifest's dropOnCopy leaves those alone).
    /// </summary>
    public static IReadOnlySet<int> RewrittenKeys => _state.RewrittenKeys;

    /// <summary>
    /// Runs the allowed extensions among those the manifests offer (ModManifests.Refresh first), loading each allowed
    /// file once per run. Never throws.
    /// </summary>
    /// <returns>How many run.</returns>
    public static int Refresh(AppSettings settings)
    {
        lock (Lock)
        {
            var offered = new List<Extension>();
            var running = new Dictionary<string, Loaded>(StringComparer.Ordinal);
            foreach (var o in ModManifests.Extensions.OrderBy(o => o.Mod, StringComparer.OrdinalIgnoreCase))
            {
                // Read once: what is hashed is what is loaded (a file rewritten in between would run unallowed).
                byte[] bytes = BytesOf(o.Dll);
                string sha = bytes.Length == 0 ? "" : Convert.ToHexStringLower(SHA256.HashData(bytes));
                if (sha == "" || !settings.Extensions.TryGetValue(o.Dll, out string? allowed) || !string.Equals(allowed, sha, StringComparison.OrdinalIgnoreCase))
                {
                    offered.Add(new Extension(o.Dll, o.Mod, sha, Status.Off, sha == "" ? "the file cannot be read" : null));
                    Console.WriteLine($"extension {o.Mod} ({Path.GetFileName(o.Dll)}): offered, off (Settings, Mod extensions).");
                    continue;
                }
                if (!LoadedFiles.TryGetValue(sha, out var made))
                {
                    made = Load(o.Dll, bytes, o.Mod);
                    LoadedFiles[sha] = made;
                }
                if (made.Loaded is { Fault: null } l)
                {
                    running[o.Dll] = l;
                    offered.Add(new Extension(o.Dll, o.Mod, sha, Status.Running, null));
                    Console.WriteLine($"extension {o.Mod} ({Path.GetFileName(o.Dll)}): running.");
                }
                else
                {
                    string why = made.Error ?? made.Loaded?.Fault ?? "it did not load";
                    offered.Add(new Extension(o.Dll, o.Mod, sha, Status.Failed, why));
                    Console.WriteLine($"extension {o.Mod} ({Path.GetFileName(o.Dll)}): not running ({why}).");
                }
            }
            _state = new State(offered, running);
            _generation++;
            return running.Count;
        }
    }

    /// <summary>
    /// Allows an offered extension to run (its file as it is now), or takes that away, in the settings (saved by the
    /// caller); it runs or stops at the next <see cref="Refresh"/>.
    /// </summary>
    public static void Allow(AppSettings settings, Extension extension, bool allow)
    {
        if (allow && extension.Sha256 != "")
        {
            settings.Extensions[extension.Dll] = extension.Sha256;
        }
        else
        {
            settings.Extensions.Remove(extension.Dll);
        }
    }

    /// <summary>A file's SHA-256 as lower-case hex; "" when it cannot be read.</summary>
    internal static string Sha256Of(string path)
    {
        byte[] bytes = BytesOf(path);
        return bytes.Length == 0 ? "" : Convert.ToHexStringLower(SHA256.HashData(bytes));
    }

    /// <summary>A file's bytes; none when it cannot be read.</summary>
    private static byte[] BytesOf(string path)
    {
        try
        {
            return File.ReadAllBytes(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    /// <summary>Loads an extension (from the bytes hashed) in a context of its own and lets it register its hooks.</summary>
    private static (Loaded? Loaded, string? Error) Load(string dll, byte[] bytes, string mod)
    {
        try
        {
            var context = new ExtensionContext(dll);
            using var stream = new MemoryStream(bytes, writable: false);
            var asm = context.LoadFromStream(stream);
            if (asm.GetCustomAttribute<WorldEditorExtensionAttribute>() is not { } mark)
            {
                return (null, "it is not marked as an extension ([assembly: WorldEditorExtension])");
            }
            if (mark.ApiVersion > SdkInfo.ApiVersion)
            {
                return (null, $"it needs a newer editor (API {mark.ApiVersion}; this one has {SdkInfo.ApiVersion})");
            }
            if (!typeof(IWorldEditorExtension).IsAssignableFrom(mark.Entry) || Activator.CreateInstance(mark.Entry) is not IWorldEditorExtension entry)
            {
                return (null, $"its entry {mark.Entry.FullName} is not an IWorldEditorExtension");
            }
            var loaded = new Loaded(mod);
            entry.Register(new Registry(loaded));
            return (loaded, null);
        }
        catch (Exception e)
        {
            // Whatever the extension's code throws while loading: it does not run.
            return (null, $"{e.GetType().Name}: {e.Message}");
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
            _generation++;
        }
    }

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

    /// <summary>Whether a running extension draws shapes at all.</summary>
    public static bool AnyShapes => _state.Hooks.Any(l => l.Shapes.Count > 0);

    /// <summary>Whether a running extension draws shapes for a prefab.</summary>
    public static bool HasShapes(int prefab) => _state.Hooks.Any(l => l.Shapes.ContainsKey(prefab));

    /// <summary>The shapes running extensions draw for an object of a prefab (none when none does).</summary>
    /// <param name="prefab">The prefab's hash.</param>
    /// <param name="obj">The object, as drawn.</param>
    /// <param name="world">The world around it (<see cref="NoWorld"/>: nothing to find).</param>
    public static List<Shape> ShapesOf(int prefab, IObjectView obj, IWorldView world)
    {
        var shapes = new List<Shape>();
        foreach (var l in _state.Hooks)
        {
            foreach (var p in l.Shapes.GetValueOrDefault(prefab) ?? [])
            {
                shapes.AddRange(Guard(l, () => p.ShapesOf(obj, world)?.Where(Valid).ToList() ?? [], []));
            }
        }
        return shapes;
    }

    /// <summary>Whether a running extension cuts the ground at all.</summary>
    public static bool AnyCuts => _state.Hooks.Any(l => l.Cutters.Count > 0);

    /// <summary>Whether a running extension cuts the ground for a prefab.</summary>
    public static bool HasCuts(int prefab) => _state.Hooks.Any(l => l.Cutters.ContainsKey(prefab));

    /// <summary>Where running extensions leave the ground undrawn for an object of a prefab (none when none does).</summary>
    /// <param name="prefab">The prefab's hash.</param>
    /// <param name="obj">The object, as drawn.</param>
    /// <param name="world">The world around it.</param>
    public static List<GroundCut> CutsOf(int prefab, IObjectView obj, IWorldView world)
    {
        var cuts = new List<GroundCut>();
        foreach (var l in _state.Hooks)
        {
            foreach (var c in l.Cutters.GetValueOrDefault(prefab) ?? [])
            {
                cuts.AddRange(Guard(l, () => c.CutsOf(obj, world)?.Where(ValidCut).ToList() ?? [], []));
            }
        }
        return cuts;
    }

    /// <summary>Whether a cut can be made: an outline of at least three finite points.</summary>
    private static bool ValidCut(GroundCut c) =>
        c.Outline is { Length: >= 3 } && c.Outline.All(p => float.IsFinite(p.X) && float.IsFinite(p.Y));

    /// <summary>The editor as extensions ask it (IEditorHost).</summary>
    public static IEditorHost Host { get; } = new EditorHost();

    /// <summary>The game the editor is connected to, as extensions see it; null when it is not.</summary>
    private static volatile ILiveGame? _live;

    /// <summary>
    /// The editor connected to a game (or opened another): the running extensions' live listeners are told, and
    /// <see cref="IEditorHost.Live"/> is it from now on. Never throws.
    /// </summary>
    public static void LiveConnected(ILiveGame live)
    {
        LiveDisconnected();
        _live = live;
        foreach (var l in _state.Hooks)
        {
            foreach (var listener in l.LiveListeners.ToList())
            {
                Guard(l, () =>
                {
                    listener.OnConnected(live);
                    return true;
                }, false);
            }
        }
    }

    /// <summary>The connection ended: the listeners are told, and <see cref="IEditorHost.Live"/> is null. Never throws.</summary>
    public static void LiveDisconnected()
    {
        if (_live == null)
        {
            return;
        }
        _live = null;
        foreach (var l in _state.Hooks)
        {
            foreach (var listener in l.LiveListeners.ToList())
            {
                Guard(l, () =>
                {
                    listener.OnDisconnected();
                    return true;
                }, false);
            }
        }
    }

    /// <summary>Asks for the shapes and cuts to be made again (IEditorHost.Redraw): a generation more.</summary>
    public static void Redraw() => _generation++;

    /// <summary>What an extension asks of the editor.</summary>
    private sealed class EditorHost : IEditorHost
    {
        /// <inheritdoc/>
        public ILiveGame? Live => _live;

        /// <inheritdoc/>
        public void Redraw() => ModExtensions.Redraw();
    }

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

    /// <summary>Whether a shape can be drawn: whole triangles of corners it has, finite corners.</summary>
    private static bool Valid(Shape s) =>
        s.Vertices != null && s.Triangles != null && s.Triangles.Length % 3 == 0
        && s.Triangles.All(i => i >= 0 && i < s.Vertices.Length)
        && s.Vertices.All(v => float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z));

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
    /// Extensions in use for the tests (registered in code, not loaded), until <see cref="Forget"/>. One entry given
    /// twice is one extension at two paths, as one file offered by two mods: registered once.
    /// </summary>
    internal static void UseForTests(params (string Mod, IWorldEditorExtension Entry)[] extensions)
    {
        lock (Lock)
        {
            var running = new Dictionary<string, Loaded>(StringComparer.Ordinal);
            var made = new Dictionary<IWorldEditorExtension, Loaded>(ReferenceEqualityComparer.Instance);
            foreach (var (mod, entry) in extensions)
            {
                if (!made.TryGetValue(entry, out var l))
                {
                    l = new Loaded(mod);
                    entry.Register(new Registry(l));
                    made[entry] = l;
                }
                running[mod] = l;
            }
            _state = new State([.. running.Keys.Select(m => new Extension(m, m, "", Status.Running, null))], running);
            _generation++;
        }
    }

    /// <summary>No extension in use (the tests).</summary>
    internal static void Forget()
    {
        lock (Lock)
        {
            _state = new State([], new Dictionary<string, Loaded>());
            _generation++;
        }
    }

    /// <summary>Where one extension registers its hooks.</summary>
    private sealed class Registry(Loaded loaded) : IExtensionRegistry
    {
        /// <inheritdoc/>
        public int ApiVersion => SdkInfo.ApiVersion;

        /// <inheritdoc/>
        public string EditorVersion => typeof(ModExtensions).Assembly.GetName().Version?.ToString(3) ?? "";

        /// <inheritdoc/>
        public void AddDecoder(string key, IDataDecoder decoder) => loaded.Decoders[StableHash.Of(key)] = decoder;

        /// <inheritdoc/>
        public void AddCopyRewriter(ICopyRewriter rewriter, params string[] keys) => loaded.Rewriters.Add((rewriter, [.. keys.Select(StableHash.Of)]));

        /// <inheritdoc/>
        public void AddShapes(IShapeProvider provider, params string[] prefabs)
        {
            foreach (string p in prefabs)
            {
                int h = StableHash.Of(p);
                (loaded.Shapes.TryGetValue(h, out var list) ? list : loaded.Shapes[h] = []).Add(provider);
            }
        }

        /// <inheritdoc/>
        public void AddGroundCuts(IGroundCutter cutter, params string[] prefabs)
        {
            foreach (string p in prefabs)
            {
                int h = StableHash.Of(p);
                (loaded.Cutters.TryGetValue(h, out var list) ? list : loaded.Cutters[h] = []).Add(cutter);
            }
        }

        /// <inheritdoc/>
        public void AddLiveListener(ILiveListener listener) => loaded.LiveListeners.Add(listener);

        /// <inheritdoc/>
        public IEditorHost Host => ModExtensions.Host;

        /// <inheritdoc/>
        public void Log(string message) => Console.WriteLine($"extension {loaded.Mod}: {message}");
    }

    /// <summary>
    /// An extension's own load context: the SDK is the editor's (one set of types), the rest the framework's. Nothing
    /// is loaded from the extension's folder: the player allowed one file, and another DLL there (a helper the
    /// extension references, changed by an update) would run unallowed. An extension is one DLL.
    /// </summary>
    private sealed class ExtensionContext(string dll) : AssemblyLoadContext("extension " + Path.GetFileName(dll))
    {
        /// <inheritdoc/>
        protected override Assembly? Load(AssemblyName name) =>
            name.Name == typeof(IWorldEditorExtension).Assembly.GetName().Name ? typeof(IWorldEditorExtension).Assembly : null;
    }
}
