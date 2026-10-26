using ValheimWorldEditor.Sdk;

namespace TerrainEditor.App;

/// <summary>
/// Putting extensions in use without loading their DLLs, for the tests.
/// </summary>
public static partial class ModExtensions
{
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
            _decided = true;
            Interlocked.Increment(ref _generation);
        }
    }

    /// <summary>No extension in use (the tests).</summary>
    internal static void Forget()
    {
        lock (Lock)
        {
            _state = new State([], new Dictionary<string, Loaded>());
            _decided = true;
            Interlocked.Increment(ref _generation);
        }
    }
}
