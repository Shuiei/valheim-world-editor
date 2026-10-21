using System.Security.Cryptography;
using ValheimWorldEditor.Sdk;

namespace TerrainEditor.App;

/// <summary>
/// Mods' editor extensions (ValheimWorldEditor.Sdk; docs/modding.md#extensions): a DLL a mod offers in its
/// worldeditor.json (ModManifests.Extensions), which runs in the editor only once the user allows it in Settings, and
/// then only that file (an add-on's, AddOns, runs as installed: the player put it there) (AppSettings.Extensions keeps its SHA-256: a changed DLL is other code and asks again; the
/// bytes hashed are the bytes loaded). Each is one DLL, loaded in a context of its own that gives it the editor's SDK
/// and the framework and nothing from its folder, and registers hooks: byte decoders for the inspector,
/// rewriters of pasted copies, shapes to draw, ground cuts, live listeners (LiveConnected). Read as a world or the Workshop opens, after the manifests. A hook that
/// throws turns its extension off until the editor starts again; the editor never fails because of one.
/// </summary>
public static partial class ModExtensions
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
    /// <param name="AddOn">An add-on's (AddOns): run without being allowed, not listed with the mods' switches.</param>
    public sealed record Extension(string Dll, string Mod, string Sha256, Status State, string? Error, bool AddOn = false);

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

        /// <summary>Its scene providers.</summary>
        public List<ISceneProvider> Scenes { get; } = [];
        /// <summary>Its tools.</summary>
        public List<IEditorTool> Tools { get; } = [];
        /// <summary>Its area copiers, with their ids.</summary>
        public List<(string Id, IAreaCopier Copier)> Copiers { get; } = [];
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

    /// <summary>The game's script classes the running extensions' manifests ask for ("gameComponents": ModComponents).</summary>
    public static IReadOnlyCollection<string> RequestedComponents =>
        [.. ModManifests.Extensions.Where(o => _state.Running.ContainsKey(o.Dll)).SelectMany(o => o.GameComponents).Distinct(StringComparer.Ordinal)];

    /// <summary>The extensions the mods offer, and what became of each.</summary>
    public static IReadOnlyList<Extension> Offered => _state.Offered;

    /// <summary>
    /// Every data key a running extension rewrites in pasted copies (a manifest's dropOnCopy leaves those alone).
    /// </summary>
    public static IReadOnlySet<int> RewrittenKeys => _state.RewrittenKeys;

    /// <summary>
    /// Runs the allowed extensions, and the add-ons' extensions, among those the manifests offer (ModManifests.Refresh
    /// first), loading each file once per run. Never throws.
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
                string what = o.AddOn ? "add-on" : "extension";
                if (sha == "" || !o.AddOn && !IsAllowed(settings, o.Dll, sha))
                {
                    offered.Add(new Extension(o.Dll, o.Mod, sha, Status.Off, sha == "" ? "the file cannot be read" : null, o.AddOn));
                    Console.WriteLine($"{what} {o.Mod} ({Path.GetFileName(o.Dll)}): " + (sha == "" ? "the file cannot be read, off." : "offered, off (Settings, Mod extensions)."));
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
                    offered.Add(new Extension(o.Dll, o.Mod, sha, Status.Running, null, o.AddOn));
                    Console.WriteLine($"{what} {o.Mod} ({Path.GetFileName(o.Dll)}): running.");
                }
                else
                {
                    string why = made.Error ?? made.Loaded?.Fault ?? "it did not load";
                    offered.Add(new Extension(o.Dll, o.Mod, sha, Status.Failed, why, o.AddOn));
                    Console.WriteLine($"{what} {o.Mod} ({Path.GetFileName(o.Dll)}): not running ({why}).");
                }
            }
            _state = new State(offered, running);
            Interlocked.Increment(ref _generation);
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

    /// <summary>Whether the settings allow a mod's extension, its file as it is (its SHA-256).</summary>
    private static bool IsAllowed(AppSettings settings, string dll, string sha) =>
        settings.Extensions.TryGetValue(dll, out string? allowed) && string.Equals(allowed, sha, StringComparison.OrdinalIgnoreCase);

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
            Log.Caught(e, "reading an extension's DLL (taken as empty: not run)");
            return [];
        }
    }
}
