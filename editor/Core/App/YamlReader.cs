using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.Loader;

namespace TerrainEditor.App;

/// <summary>
/// Reads YAML through YamlDotNet, the library Expand World Data's files are written and read with. The editor ships no
/// copy of it: the mod depends on it, so the player's BepInEx folder has one (the ValheimModding-YamlDotNet package
/// under plugins), and that very file is loaded, into its own load context, and driven by reflection. A document is
/// read as plain lists and maps of strings (<see cref="ReadList"/>), never into typed objects, so any version of the
/// library the mod runs with will do.
/// </summary>
public sealed class YamlReader
{
    /// <summary>The library's file name under a BepInEx plugins folder.</summary>
    public const string DllName = "YamlDotNet.dll";

    /// <summary>The readers made so far, by the library's full path (one load context each).</summary>
    private static readonly ConcurrentDictionary<string, YamlReader> Loaded = new();

    /// <summary>The library file this reader runs.</summary>
    public string Path { get; }

    /// <summary>Makes the library's deserializer (one a read: the library does not promise one is safe to share).</summary>
    private readonly Func<object> _newDeserializer;

    /// <summary>The deserializer's Deserialize(string, Type).</summary>
    private readonly MethodInfo _deserialize;

    /// <summary>A reader over a loaded library.</summary>
    private YamlReader(string path, Func<object> newDeserializer, MethodInfo deserialize)
    {
        Path = path;
        _newDeserializer = newDeserializer;
        _deserialize = deserialize;
    }

    /// <summary>
    /// The library's file under a BepInEx plugins folder (the first found in name order, at any depth); null when
    /// there is none, or the folder cannot be searched (said in the log).
    /// </summary>
    /// <param name="plugins">The plugins folder.</param>
    public static string? FindDll(string plugins)
    {
        try
        {
            if (!Directory.Exists(plugins))
            {
                return null;
            }
            return Directory.EnumerateFiles(plugins, DllName, new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = 0 })
                .OrderBy(p => p, StringComparer.Ordinal).FirstOrDefault();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Caught(ex, $"searching a plugins folder for {DllName} (taken as not there)");
            return null;
        }
    }

    /// <summary>
    /// A reader over the library found under a BepInEx plugins folder; null when there is none, or it cannot be loaded
    /// (said in the log).
    /// </summary>
    /// <param name="plugins">The plugins folder.</param>
    public static YamlReader? Find(string plugins)
    {
        string? dll = FindDll(plugins);
        if (dll == null)
        {
            return null;
        }
        try
        {
            return Load(dll);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or BadImageFormatException or TypeLoadException or MissingMemberException or InvalidOperationException)
        {
            Log.Caught(ex, $"loading {dll} (Expand World Data's files cannot be read without it)");
            return null;
        }
    }

    /// <summary>
    /// A reader over a library file, loaded once per file into its own load context.
    /// </summary>
    /// <param name="dllPath">The library file.</param>
    /// <exception cref="FileNotFoundException">There is no such file.</exception>
    /// <exception cref="IOException">The file cannot be read.</exception>
    /// <exception cref="BadImageFormatException">The file is not a .NET library.</exception>
    /// <exception cref="TypeLoadException">The library has not the types the reader drives.</exception>
    /// <exception cref="MissingMemberException">The library's deserializer has not the method the reader drives.</exception>
    public static YamlReader Load(string dllPath) => Loaded.GetOrAdd(System.IO.Path.GetFullPath(dllPath), LoadNew);

    /// <summary>
    /// Loads a library file into a new load context and finds the deserializer in it. The file's bytes are loaded,
    /// not the file itself: loaded from its path, the file stays mapped and locked as long as the editor runs, which
    /// on Windows keeps a mod manager from updating the player's library (and a test from deleting its copy).
    /// </summary>
    private static YamlReader LoadNew(string fullPath)
    {
        var context = new AssemblyLoadContext("YamlDotNet from " + fullPath);
        using var bytes = new MemoryStream(File.ReadAllBytes(fullPath));
        Assembly asm = context.LoadFromStream(bytes);
        Type builder = asm.GetType("YamlDotNet.Serialization.DeserializerBuilder", throwOnError: true)!;
        Type deserializer = asm.GetType("YamlDotNet.Serialization.IDeserializer", throwOnError: true)!;
        MethodInfo build = builder.GetMethod("Build", Type.EmptyTypes) ?? throw new MissingMethodException(builder.FullName, "Build");
        MethodInfo deserialize = deserializer.GetMethod("Deserialize", [typeof(string), typeof(Type)]) ?? throw new MissingMethodException(deserializer.FullName, "Deserialize");
        object NewDeserializer() => build.Invoke(Activator.CreateInstance(builder), null) ?? throw new InvalidOperationException("YamlDotNet's builder built nothing");
        return new YamlReader(fullPath, NewDeserializer, deserialize);
    }

    /// <summary>
    /// A YAML document that is a list of maps, as plain values: each map's keys as written, scalars as their text,
    /// nested lists and maps as lists and maps of the same. Entries that are not maps are passed over; an empty
    /// document is an empty list.
    /// </summary>
    /// <param name="text">The document.</param>
    /// <exception cref="InvalidDataException">The document is not YAML the library reads, with its message.</exception>
    public List<Dictionary<string, object?>> ReadList(string text)
    {
        object? root;
        try
        {
            root = _deserialize.Invoke(_newDeserializer(), [text, typeof(object)]);
        }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            throw new InvalidDataException(ex.InnerException.Message, ex.InnerException);
        }
        List<Dictionary<string, object?>> list = [];
        if (root is System.Collections.IEnumerable items and not string)
        {
            foreach (object? item in items)
            {
                if (Plain(item) is Dictionary<string, object?> map)
                {
                    list.Add(map);
                }
            }
        }
        return list;
    }

    /// <summary>A node of the library's object graph as plain values (maps keyed by their keys' text, lists, strings).</summary>
    private static object? Plain(object? node)
    {
        switch (node)
        {
            case null:
                return null;
            case string s:
                return s;
            case System.Collections.IDictionary dict:
            {
                Dictionary<string, object?> map = [];
                foreach (System.Collections.DictionaryEntry entry in dict)
                {
                    map[entry.Key.ToString() ?? ""] = Plain(entry.Value);
                }
                return map;
            }
            case System.Collections.IEnumerable items:
            {
                List<object?> list = [];
                foreach (object? item in items)
                {
                    list.Add(Plain(item));
                }
                return list;
            }
            default:
                return node.ToString();
        }
    }
}
