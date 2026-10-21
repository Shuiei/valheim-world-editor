using System.Reflection;
using System.Runtime.Loader;
using TerrainEditor.Save;
using ValheimWorldEditor.Sdk;

namespace TerrainEditor.App;

/// <summary>
/// Loading an allowed extension DLL from the bytes hashed, in a context of its own, and the registry it registers its hooks with.
/// </summary>
public static partial class ModExtensions
{
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
            Log.Caught(e, $"loading the extension of {mod} (not run)");
            return (null, $"{e.GetType().Name}: {e.Message}");
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
        public void AddScene(ISceneProvider provider) => loaded.Scenes.Add(provider);

        /// <inheritdoc/>
        public void AddTool(IEditorTool tool) => loaded.Tools.Add(tool);

        /// <inheritdoc/>
        public void AddAreaCopier(string id, IAreaCopier copier)
        {
            if (copier == null || id == null || !ToolIdForm().IsMatch(id))
            {
                Log($"area copier \"{id}\" left out: none given, or its id is not 1 to 64 letters, digits, dots, dashes or underscores");
                return;
            }
            loaded.Copiers.Add((id, copier));
        }

        /// <inheritdoc/>
        public void AddLiveListener(ILiveListener listener) => loaded.LiveListeners.Add(listener);

        /// <inheritdoc/>
        public void AddWorldGenerator(IWorldGenerator generator) => loaded.Generators.Add(generator);

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
