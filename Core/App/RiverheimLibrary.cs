using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.Loader;

namespace TerrainEditor.App;

/// <summary>
/// The player's Riverheim.dll, loaded to lay worlds out with Riverheim's own generator: the editor carries none of the
/// mod (it has no licence to), it calls the copy the player installed. Loaded into its own context from the file's
/// bytes (the file stays free), its own types driven by name. The plugin's classes in it name BepInEx's and Unity's
/// (no code of theirs runs): those come from the BepInEx folder's core and the game's Managed folder.
/// </summary>
/// <remarks>
/// Followed: Riverheim 1.2.1. The game side of the mod does, for a world: the preset's configuration
/// (ConfigManager.GetConfig), a pipeline that computes the "Rendering/WorldRenderer" result for the seed, then the
/// renderer's RenderPoint, GetBiome and GetForestation for each point (<see cref="RiverheimGround"/>).
/// </remarks>
internal sealed class RiverheimLibrary
{
    /// <summary>The pipeline result the game side asks for: the renderer of the laid-out world.</summary>
    private const string RendererId = "Rendering/WorldRenderer";

    /// <summary>
    /// The libraries loaded, by full path, the file's state and the folders its dependencies come from (an updated
    /// Riverheim, or another Valheim folder, is loaded anew; a failed load is tried again next time).
    /// </summary>
    private static readonly ConcurrentDictionary<string, Lazy<RiverheimLibrary>> Loaded = new(StringComparer.Ordinal);

    private readonly Type _configManager, _settings, _builder, _renderer;

    /// <summary>The worlds laid out last (two: the open world's, and one more), by seed and preset.</summary>
    private readonly List<(string Key, Lazy<RiverheimGround> Ground)> _recent = [];

    /// <summary>
    /// Loads Riverheim from its file. Throws when the file cannot be read or is not the Riverheim the editor follows.
    /// </summary>
    /// <param name="path">Riverheim.dll's full path.</param>
    /// <param name="dependencies">The folders the BepInEx and Unity assemblies it names are taken from.</param>
    private RiverheimLibrary(string path, IReadOnlyList<string> dependencies)
    {
        var context = new AssemblyLoadContext("Riverheim from " + path);
        context.Resolving += (c, name) => Dependency(c, name, dependencies);
        Assembly asm;
        using (var file = File.OpenRead(path))
        {
            asm = context.LoadFromStream(file);
        }
        Version = asm.GetName().Version is { } v ? $"{v.Major}.{v.Minor}.{v.Build}" : "?";
        _configManager = asm.GetType("Riverheim.Configuration.ConfigManager", true)!;
        _settings = asm.GetType("Riverheim.Configuration.WorldSettings", true)!;
        _builder = asm.GetType("Riverheim.Pipeline.GeneratorPipeline+Builder", true)!;
        _renderer = asm.GetType("Riverheim.Rendering.WorldRenderer", true)!;
        var point = asm.GetType("Riverheim.Rfloat2", true)!;
        Calls = new RiverheimGround.Calls(_renderer, point, asm.GetType("Riverheim.World.Biomes.Biome", true)!);
        Quiet(asm.GetType("Riverheim.Util.Debug.Logger", true)!);
    }

    /// <summary>The player's Riverheim's version (major.minor.patch).</summary>
    public string Version { get; }

    /// <summary>The renderer's calls, compiled once.</summary>
    public RiverheimGround.Calls Calls { get; }

    /// <summary>
    /// Riverheim from this file, loaded once (per path, file state and dependency folders). Throws when it cannot be
    /// loaded (the next call tries again).
    /// </summary>
    /// <param name="path">Riverheim.dll.</param>
    /// <param name="dependencies">The folders the BepInEx and Unity assemblies it names are taken from.</param>
    public static RiverheimLibrary Load(string path, IReadOnlyList<string> dependencies)
    {
        string full = Path.GetFullPath(path);
        var file = new FileInfo(full);
        string key = $"{full}|{file.LastWriteTimeUtc.Ticks}|{file.Length}|{string.Join('|', dependencies)}";
        return Loaded.GetOrAdd(key, _ => new Lazy<RiverheimLibrary>(() => new RiverheimLibrary(full, dependencies), LazyThreadSafetyMode.PublicationOnly)).Value;
    }

    /// <summary>
    /// Why this Riverheim cannot lay out a world made with these settings (an unknown preset, or a newer preset
    /// version than it knows), in its own words; null when it can.
    /// </summary>
    /// <param name="tag">The settings the world file keeps.</param>
    public string? Problem(Riverheim.Tag tag)
    {
        object?[] args = [Settings(tag), null];
        return (bool)Invoke(_configManager, "Validate", null, args)! ? null : args[1] as string ?? "";
    }

    /// <summary>
    /// The preset Riverheim gives a new world (its own, at its newest version). Throws <c>MissingMemberException</c>
    /// when this Riverheim has no such thing (another version than the one followed).
    /// </summary>
    public Riverheim.Tag DefaultTag()
    {
        object settings = Invoke(_configManager, "DefaultWorldSettings", null, []) ?? throw new MissingMemberException(_configManager.FullName, "DefaultWorldSettings");
        object? Value(string name) => (_settings.GetProperty(name) ?? throw new MissingMemberException(_settings.FullName, name)).GetValue(settings);
        return Value("Preset") is string preset && Value("PresetVersion") is int version ? new Riverheim.Tag(preset, version) : throw new MissingMemberException(_settings.FullName, "Preset");
    }

    /// <summary>
    /// The ground of a world: laid out for the seed and the world's settings (seconds of work and a couple of hundred
    /// megabytes), or taken from the last two laid out.
    /// </summary>
    /// <param name="seed">The world's seed.</param>
    /// <param name="tag">The settings the world file keeps.</param>
    public RiverheimGround Ground(int seed, Riverheim.Tag tag)
    {
        string key = $"{seed}|{tag.Preset}|{tag.PresetVersion}";
        Lazy<RiverheimGround> ground;
        lock (_recent)
        {
            int i = _recent.FindIndex(r => r.Key == key);
            if (i >= 0)
            {
                ground = _recent[i].Ground;
                _recent.RemoveAt(i);
            }
            else
            {
                // Once, however many ask at the same time (seconds of work and the memory of a world).
                ground = new Lazy<RiverheimGround>(() => LayOut(seed, tag), LazyThreadSafetyMode.ExecutionAndPublication);
            }
            _recent.Insert(0, (key, ground));
            if (_recent.Count > 2)
            {
                _recent.RemoveAt(2);
            }
        }
        try
        {
            return ground.Value;
        }
        catch (Exception e)
        {
            // A layout that failed is kept by its Lazy: forgotten, so the next open tries again.
            Log.CaughtBrief(e, $"laying out seed {seed} with Riverheim {Version} (forgotten: the next open tries again)");
            lock (_recent)
            {
                _recent.RemoveAll(r => ReferenceEquals(r.Ground, ground));
            }
            throw;
        }
    }

    /// <summary>Lays a world out, as the game side does in its Pregenerate prefix.</summary>
    private RiverheimGround LayOut(int seed, Riverheim.Tag tag)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        object config = Invoke(_configManager, "GetConfig", null, [Settings(tag)])!;
        object builder = Activator.CreateInstance(_builder, [new[] { RendererId }])!;
        builder = Invoke(_builder, "SetParallel", builder, [true])!;
        builder = Invoke(_builder, "SetClearUnneededCache", builder, [true])!;
        object pipeline = Invoke(_builder, "Build", builder, [])!;
        pipeline.GetType().GetMethod("Compute", [typeof(int), typeof(object)])!.Invoke(pipeline, [seed, config]);
        object renderer = pipeline.GetType().GetMethod("GetResult")!.MakeGenericMethod(_renderer).Invoke(pipeline, [RendererId])!;
        Console.WriteLine($"Riverheim {Version}: seed {seed} laid out with preset {tag.Preset} v{tag.PresetVersion} in {watch.ElapsedMilliseconds} ms.");
        return new RiverheimGround(Calls, renderer);
    }

    /// <summary>The settings as Riverheim's own record.</summary>
    private object Settings(Riverheim.Tag tag) => Activator.CreateInstance(_settings, [tag.Preset, tag.PresetVersion])!;

    /// <summary>
    /// Calls a public method by name; the method's own exception is thrown as it is, and <c>MissingMethodException</c>
    /// when there is no such method (another version than the one followed).
    /// </summary>
    private static object? Invoke(Type type, string method, object? target, object?[] args)
    {
        try
        {
            var found = type.GetMethod(method, BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance) ?? throw new MissingMethodException(type.FullName, method);
            return found.Invoke(target, args);
        }
        catch (TargetInvocationException e) when (e.InnerException != null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw(e.InnerException);
            throw;
        }
    }

    /// <summary>An assembly Riverheim names, from the first folder that has it (none: it stays missing).</summary>
    private static Assembly? Dependency(AssemblyLoadContext context, AssemblyName name, IReadOnlyList<string> folders)
    {
        foreach (string folder in folders)
        {
            string file = Path.Combine(folder, name.Name + ".dll");
            if (File.Exists(file))
            {
                using var stream = File.OpenRead(file);
                return context.LoadFromStream(stream);
            }
        }
        Console.WriteLine($"Riverheim: {name.Name}.dll is in none of {string.Join(", ", folders)}.");
        return null;
    }

    /// <summary>
    /// Riverheim's own log, into the editor's: its warnings and errors (each stage it computes, said as information,
    /// is left out).
    /// </summary>
    private static void Quiet(Type logger)
    {
        var set = logger.GetMethod("SetLogger")!;
        var action = set.GetParameters()[0].ParameterType;
        var parameters = action.GetMethod("Invoke")!.GetParameters();
        var level = Expression.Parameter(parameters[0].ParameterType, "level");
        var message = Expression.Parameter(typeof(object), "message");
        var call = Expression.Call(typeof(RiverheimLibrary).GetMethod(nameof(Said), BindingFlags.NonPublic | BindingFlags.Static)!, Expression.Convert(level, typeof(int)), message);
        set.Invoke(null, [Expression.Lambda(action, call, level, message).Compile()]);
    }

    /// <summary>A line of Riverheim's log: its warnings (1) and errors (2) said; its information (0) left out.</summary>
    private static void Said(int level, object message)
    {
        if (level > 0)
        {
            Console.WriteLine($"Riverheim {(level == 1 ? "warning" : "error")}: {message}");
        }
    }
}
