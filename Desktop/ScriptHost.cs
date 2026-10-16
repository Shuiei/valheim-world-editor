using System.Numerics;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using TerrainEditor.Terrain;

namespace TerrainEditor.Desktop;

/// <summary>
/// The Script tool's engine: a C# script (top-level statements, with the editor's API: ScriptApi.csx) is compiled in
/// the app (Roslyn, against .NET's reference assemblies carried inside), then run against a snapshot of the open area
/// in a process of its own (RunInChild, in ScriptHost.Child.cs), so that Stop can always end it. What it changes
/// (ground, paint, objects) comes back and goes into the area afterwards as one undo step (Apply), on the window's
/// thread.
/// </summary>
public static partial class ScriptHost
{
    /// <summary>The script API's source (ScriptApi.csx), embedded in the app; read once.</summary>
    private static readonly Lazy<string> Api = new(() =>
    {
        using Stream s = typeof(ScriptHost).Assembly.GetManifestResourceStream("TerrainEditor.ScriptApi.csx")
            ?? throw new InvalidOperationException("ScriptApi.csx is not embedded");
        using var r = new StreamReader(s);
        return r.ReadToEnd();
    });

    /// <summary>The API scripts are compiled with, as its source (Claude's script_reference).</summary>
    internal static string ApiSource => Api.Value;

    /// <summary>The usings every script gets, the API's globals among them.</summary>
    private const string Usings = "global using System;\nglobal using System.Linq;\nglobal using System.Collections.Generic;\nglobal using static Globals;\n";

    /// <summary>
    /// Compiles a script: its assembly's bytes and debug information (for the line an error happens on), or its
    /// mistakes ("line 3: ..." for the script's own lines; 20 at most). Image and Pdb are null when it does not
    /// compile.
    /// </summary>
    public static (byte[]? Image, byte[]? Pdb, List<string> Errors) Compile(string code)
    {
        var parse = new CSharpParseOptions(LanguageVersion.CSharp12);
        // With an encoding: the debug information (an error's line) needs one.
        SyntaxTree Tree(string text, string path) => CSharpSyntaxTree.ParseText(Microsoft.CodeAnalysis.Text.SourceText.From(text, Encoding.UTF8), parse, path);
        var user = Tree(code, "script");
        // Every loop of the script's own stops when asked (Stop, the 2-minute limit). A script with syntax mistakes is
        // left as it is: it does not compile anyway, and its mistakes are reported as written.
        if (!user.GetDiagnostics().Any(d => d.Severity == DiagnosticSeverity.Error))
        {
            user = Tree(new LoopChecks().Visit(user.GetRoot()).ToFullString(), "script");
        }
        var compilation = CSharpCompilation.Create("Script" + Guid.NewGuid().ToString("N")[..8],
            [Tree(Api.Value, "api"), Tree(Usings, "usings"), user],
            Basic.Reference.Assemblies.Net100.References.All,
            new CSharpCompilationOptions(OutputKind.ConsoleApplication, optimizationLevel: OptimizationLevel.Release, nullableContextOptions: NullableContextOptions.Disable));
        using var ms = new MemoryStream();
        using var pdb = new MemoryStream();
        var result = compilation.Emit(ms, pdb, options: new Microsoft.CodeAnalysis.Emit.EmitOptions(debugInformationFormat: Microsoft.CodeAnalysis.Emit.DebugInformationFormat.PortablePdb));
        var errors = result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Select(d =>
        {
            var span = d.Location.GetLineSpan();
            return span.Path == "script" ? $"line {span.StartLinePosition.Line + 1}: {d.GetMessage()}" : d.GetMessage();
        }).Distinct().Take(20).ToList();
        return (result.Success ? ms.ToArray() : null, result.Success ? pdb.ToArray() : null, errors);
    }

    /// <summary>
    /// Puts a call to the API's Check at the start of every loop's body and before every goto in a script, so that
    /// Stop and the 2-minute limit end a script whose own loops call nothing that checks. What it adds holds no line
    /// break: the script keeps its line numbers (for the line an error happens on).
    /// </summary>
    private sealed class LoopChecks : CSharpSyntaxRewriter
    {
        /// <summary>The call added, parsed once.</summary>
        private static readonly StatementSyntax Check = SyntaxFactory.ParseStatement("global::__Host.Check();");

        /// <summary>
        /// The statement with the Check before it, both in a block of their own. The statement's trivia (comments,
        /// line breaks, #if and other directives) goes around the block, not into it: a directive stays at the start
        /// of its own line.
        /// </summary>
        private static BlockSyntax Checked(StatementSyntax statement) =>
            SyntaxFactory.Block(SyntaxFactory.Token(default, SyntaxKind.OpenBraceToken, default), [Check, statement.WithoutTrivia()], SyntaxFactory.Token(default, SyntaxKind.CloseBraceToken, default))
                .WithLeadingTrivia(statement.GetLeadingTrivia()).WithTrailingTrivia(statement.GetTrailingTrivia());

        /// <inheritdoc/>
        public override SyntaxNode? VisitWhileStatement(WhileStatementSyntax node)
        {
            var n = (WhileStatementSyntax)base.VisitWhileStatement(node)!;
            return n.WithStatement(Checked(n.Statement));
        }

        /// <inheritdoc/>
        public override SyntaxNode? VisitDoStatement(DoStatementSyntax node)
        {
            var n = (DoStatementSyntax)base.VisitDoStatement(node)!;
            return n.WithStatement(Checked(n.Statement));
        }

        /// <inheritdoc/>
        public override SyntaxNode? VisitForStatement(ForStatementSyntax node)
        {
            var n = (ForStatementSyntax)base.VisitForStatement(node)!;
            return n.WithStatement(Checked(n.Statement));
        }

        /// <inheritdoc/>
        public override SyntaxNode? VisitForEachStatement(ForEachStatementSyntax node)
        {
            var n = (ForEachStatementSyntax)base.VisitForEachStatement(node)!;
            return n.WithStatement(Checked(n.Statement));
        }

        /// <inheritdoc/>
        public override SyntaxNode? VisitForEachVariableStatement(ForEachVariableStatementSyntax node)
        {
            var n = (ForEachVariableStatementSyntax)base.VisitForEachVariableStatement(node)!;
            return n.WithStatement(Checked(n.Statement));
        }

        /// <inheritdoc/>
        public override SyntaxNode? VisitGotoStatement(GotoStatementSyntax node)
        {
            return Checked((GotoStatementSyntax)base.VisitGotoStatement(node)!);
        }
    }

    /// <summary>
    /// What a script did: the ground's new heights (grid points changed), paint, objects taken away (indices into the
    /// scene's things) and new ones (NaN height: on the ground at the end).
    /// </summary>
    public sealed class Changes
    {
        /// <summary>New heights by grid point.</summary>
        public Dictionary<int, float> Heights { get; } = [];
        /// <summary>New paint by grid point: four weights from 0 to 1.</summary>
        public Dictionary<int, float[]> Paint { get; } = [];
        /// <summary>Things to take away, indices into the scene's things.</summary>
        public HashSet<int> Remove { get; } = [];
        /// <summary>
        /// New objects: prefab hash and name, world position (NaN height: on the ground at the end), rotation (Euler
        /// degrees), scale, and whether it snaps to the pieces there as the hammer does.
        /// </summary>
        public List<(int Prefab, string Name, Vector3 Position, Vector3 Rotation, float Scale, bool Snap)> Add { get; } = [];
        /// <summary>Things moved, turned or sized: index into the scene's things, its new place, rotation and scale.</summary>
        public Dictionary<int, (Vector3 Position, Vector3 Rotation, float Scale)> Moves { get; } = [];
        /// <summary>What the script printed (2000 lines at most).</summary>
        public StringBuilder Output { get; } = new();
        /// <summary>
        /// Whether the heights may go past the game's ±8 m (the script's Ground.NoLimit when it ended).
        /// </summary>
        public bool NoLimit { get; set; } = true;
        /// <summary>The script changed nothing.</summary>
        public bool Empty => Heights.Count == 0 && Paint.Count == 0 && Remove.Count == 0 && Add.Count == 0 && Moves.Count == 0;
    }

    /// <summary>The area as the script sees it: taken on the window's thread before the script runs.</summary>
    /// <param name="W">Grid points across (x).</param>
    /// <param name="H">Grid points deep (z).</param>
    /// <param name="Ox">World x of the first grid point.</param>
    /// <param name="Oz">World z of the first grid point.</param>
    /// <param name="Heights">The heights now, by grid point.</param>
    /// <param name="Original">The heights as the world generator made them.</param>
    /// <param name="Biomes">The biome of each grid point.</param>
    /// <param name="Mask">The paint, four bytes per grid point.</param>
    /// <param name="Locked">Grid points the script must leave alone.</param>
    /// <param name="Things">The objects standing: index into the scene's things, prefab name, position, kind of object,
    /// whether it is a player-built piece, its rotation (Euler degrees) and scale (zero: the prefab's own).</param>
    /// <param name="Water">The water level.</param>
    /// <param name="Seed">The world's seed (0 without a world).</param>
    /// <param name="World">The world's name.</param>
    /// <param name="Creatable">The prefabs (by hash) the editor can make: the world's own and the game's placeable
    /// kinds and creatures.</param>
    public sealed record Snapshot(int W, int H, float Ox, float Oz, float[] Heights, float[] Original, int[] Biomes, byte[] Mask, bool[] Locked,
        List<(int Index, string Prefab, Vector3 Position, string Kind, bool Piece, Vector3 Rotation, float Scale)> Things, float Water, int Seed, string World, HashSet<int> Creatable)
    {
        /// <summary>Whether the editor can make a prefab (by hash).</summary>
        public bool CanCreate(int prefab) => Creatable.Contains(prefab);
    }

    /// <summary>Takes the snapshot of the session's area, on the window's thread.</summary>
    /// <param name="s">The edit session.</param>
    /// <param name="nameOf">A prefab hash's name (null when unknown).</param>
    public static Snapshot Take(EditSession s, Func<int, string?> nameOf)
    {
        var g = s.Ground;
        var scene = s.Scene;
        float[] original = new float[g.W * g.H];
        bool[] locked = new bool[g.W * g.H];
        for (int p = 0; p < original.Length; p++)
        {
            original[p] = g.Original(p);
            locked[p] = g.Locked(p % g.W, p / g.W);
        }
        List<(int, string, Vector3, string, bool, Vector3, float)> things;
        // Name and kind once per kind of thing, not per thing: an area can hold millions, and a kind is
        // found by matching the name against patterns.
        var known = new Dictionary<(int, bool, bool), (string Name, string Kind)>();
        lock (scene.Things)
        {
            things = new(scene.Things.Count);
            for (int i = 0; i < scene.Things.Count; i++)
            {
                var t = scene.Things[i];
                if (t.Gone)
                {
                    continue;
                }
                if (!known.TryGetValue((t.Prefab, t.Piece, t.Tamed), out var nk))
                {
                    string? name = nameOf(t.Prefab);
                    nk = known[(t.Prefab, t.Piece, t.Tamed)] = (name ?? "", ObjectKinds.Of(name, t.Piece, t.Tamed).ToString());
                }
                things.Add((i, nk.Name, t.Position, nk.Kind, t.Piece, t.Rotation, t.Scale));
            }
        }
        var world = scene.World;
        // As WorldSave.CanCreate: the world's own objects, the game's placeable kinds and its creatures; without a
        // world (the Workshop), the placeable kinds.
        HashSet<int> creatable = world != null
            ? [.. world.Creatable, .. PrefabCatalog.Creatures]
            : [.. PrefabCatalog.Placeable.Select(p => StableHash.Of(p.Name))];
        return new Snapshot(g.W, g.H, scene.X0 * 64f - 32f, scene.Z0 * 64f - 32f, (float[])scene.Heights.Clone(), original, (int[])scene.Biomes.Clone(), (byte[])scene.Mask.Clone(), locked,
            things, scene.Water, world?.Seed ?? 0, world?.Name ?? "", creatable);
    }

    /// <summary>
    /// Runs the compiled script against the snapshot in this process, on the calling thread (call it on a background
    /// one), with its new pieces snapped (SnapPieces). The editor runs scripts in a process of their own instead
    /// (RunInChild), which Stop can always end.
    /// </summary>
    /// <param name="image">The compiled assembly.</param>
    /// <param name="pdb">Its debug information, or null (errors then have no line).</param>
    /// <param name="snap">The area as the script sees it.</param>
    /// <param name="cancel">Stops the script at its next Check or Mountain row.</param>
    /// <returns>What the script changed; the script's own exceptions are thrown on.</returns>
    public static Changes Run(byte[] image, byte[]? pdb, Snapshot snap, CancellationToken cancel)
    {
        var ch = Execute(image, pdb, snap, cancel);
        SnapPieces(snap, ch, cancel);
        return ch;
    }

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
        float[] heights = (float[])snap.Heights.Clone();
        int lines = 0;
        var noises = new Dictionary<int, Noise>();
        int Point(float x, float z)
        {
            // Not a point at all (NaN, infinity): would turn into grid column or row 0.
            if (!float.IsFinite(x) || !float.IsFinite(z))
            {
                return -1;
            }
            int gx = (int)MathF.Round(x - snap.Ox), gz = (int)MathF.Round(z - snap.Oz);
            return gx < 0 || gz < 0 || gx >= snap.W || gz >= snap.H ? -1 : gz * snap.W + gx;
        }
        // Between grid points, the height in between (as the ground is drawn).
        float Sample(float[] a, float x, float z)
        {
            float fx = Math.Clamp(x - snap.Ox, 0, snap.W - 1), fz = Math.Clamp(z - snap.Oz, 0, snap.H - 1);
            int x0 = Math.Min((int)fx, snap.W - 2), z0 = Math.Min((int)fz, snap.H - 2);
            float u = fx - x0, v = fz - z0;
            float h00 = a[z0 * snap.W + x0], h10 = a[z0 * snap.W + x0 + 1], h01 = a[(z0 + 1) * snap.W + x0], h11 = a[(z0 + 1) * snap.W + x0 + 1];
            return (h00 * (1 - u) + h10 * u) * (1 - v) + (h01 * (1 - u) + h11 * u) * v;
        }
        // The callbacks that change something hold the changes' lock: a script may call them from several threads
        // (Parallel.For), and the changes are dictionaries and lists.
        void SetHeight(float x, float z, float h)
        {
            int p = Point(x, z);
            if (p >= 0 && !snap.Locked[p] && float.IsFinite(h))
            {
                lock (ch)
                {
                    heights[p] = h;
                    ch.Heights[p] = h;
                }
            }
        }
        var alc = new AssemblyLoadContext("Script", isCollectible: true);
        try
        {
            var asm = pdb != null ? alc.LoadFromStream(new MemoryStream(image), new MemoryStream(pdb)) : alc.LoadFromStream(new MemoryStream(image));
            var host = asm.GetType("__Host") ?? throw new InvalidOperationException("the script's API is missing");
            void Fill(string name, object? value) => host.GetField(name, BindingFlags.Public | BindingFlags.Static)!.SetValue(null, value);
            Fill("Height", new Func<float, float, float>((x, z) => Sample(heights, x, z)));
            Fill("Original", new Func<float, float, float>((x, z) => Sample(snap.Original, x, z)));
            Fill("Set", new Action<float, float, float>(SetHeight));
            Fill("Biome", new Func<float, float, string>((x, z) => Point(x, z) is int p and >= 0 ? ((ValheimGen.Heightmap.Biome)snap.Biomes[p]).ToString() : "None"));
            Fill("Paint", new Action<float, float, string, float>((x, z, kind, strength) =>
            {
                float[] colour = kind.ToLowerInvariant() switch
                {
                    "dirt" => Brush.PaintOf(BrushTool.PaintDirt)!,
                    "cultivated" or "cultivate" => Brush.PaintOf(BrushTool.PaintCultivated)!,
                    "paved" => Brush.PaintOf(BrushTool.PaintPaved)!,
                    "clear" or "none" => Brush.PaintOf(BrushTool.PaintClear)!,
                    _ => throw new ArgumentException($"no paint “{kind}” (dirt, cultivated, paved or clear)"),
                };
                int p = Point(x, z);
                if (p < 0 || snap.Locked[p])
                {
                    return;
                }
                lock (ch)
                {
                    float[] now = ch.Paint.TryGetValue(p, out float[]? c) ? c : [.. Enumerable.Range(0, 4).Select(i => snap.Mask[p * 4 + i] / 255f)];
                    float k = Math.Clamp(strength, 0, 1);
                    ch.Paint[p] = [.. now.Select((v, i) => v + (colour[i] - v) * k)];
                }
            }));
            Fill("Mountain", new Action<float, float, string, float, float, float, float, int>((x, z, preset, height, radius, rough, turn, seed) =>
            {
                var pre = Mountain.Presets.FirstOrDefault(m => string.Equals(m.Name, preset, StringComparison.OrdinalIgnoreCase))
                    ?? throw new ArgumentException($"no mountain preset “{preset}” ({string.Join(", ", Mountain.Presets.Select(m => m.Name))})");
                var rolled = Mountain.Roll(pre, seed != 0 ? new Random(seed) : new Random());
                var spec = rolled with
                {
                    Height = height > 0 ? height : rolled.Height,
                    Radius = radius > 0 ? radius : rolled.Radius,
                    Rough = rough >= 0 ? rough : rolled.Rough,
                    Turn = turn >= 0 ? turn : rolled.Turn,
                };
                var shape = Mountain.Shape(spec);
                float reach = spec.Reach;
                // Counted, and only where the area is: a float counter stops growing past 2^24 m.
                var (fromZ, toZ) = Offsets(z, reach, snap.Oz - 1, snap.Oz + snap.H);
                var (fromX, toX) = Offsets(x, reach, snap.Ox - 1, snap.Ox + snap.W);
                for (int dz = fromZ; dz <= toZ; dz++)
                {
                    cancel.ThrowIfCancellationRequested();
                    for (int dx = fromX; dx <= toX; dx++)
                    {
                        float v = shape(dx, dz);
                        if (v != 0 && Point(x + dx, z + dz) is int p and >= 0)
                        {
                            // Read and written under the lock, so two mountains at once both count.
                            lock (ch)
                            {
                                SetHeight(x + dx, z + dz, heights[p] + v);
                            }
                        }
                    }
                }
                // The trees and rocks it buries go, as with the Mountain tool.
                foreach (var t in snap.Things)
                {
                    if (!t.Piece && t.Kind is "Trees" or "Rocks" or "Ore" or "Bushes" or "Pickables" && shape(t.Position.X - x, t.Position.Z - z) > Uplift.ClearHeight)
                    {
                        lock (ch)
                        {
                            ch.Remove.Add(t.Index);
                        }
                    }
                }
            }));
            Fill("Objects", new Func<(int, string, float, float, float, string, bool, float, float, float, float)[]>(() =>
                [.. snap.Things.Select(t => (t.Index, t.Prefab, t.Position.X, t.Position.Y, t.Position.Z, t.Kind, t.Piece, t.Rotation.X, t.Rotation.Y, t.Rotation.Z, t.Scale > 0 ? t.Scale : 1))]));
            Fill("Remove", new Action<int>(i =>
            {
                lock (ch)
                {
                    ch.Remove.Add(i);
                }
            }));
            Fill("CanPlace", new Func<string, bool>(name => snap.CanCreate(StableHash.Of(name))));
            Fill("Place", new Action<string, float, float, float, float, float, float, float, bool>((name, x, z, yaw, scale, y, pitch, roll, snapIt) =>
            {
                lock (ch)
                {
                    Place(snap, ch, name, x, z, yaw, scale, y, pitch, roll, snapIt);
                }
            }));
            Fill("Move", new Action<int, float, float, float, float, float, float, float>((index, x, z, y, yaw, pitch, roll, scale) =>
            {
                lock (ch)
                {
                    Move(snap, ch, index, x, z, y, yaw, pitch, roll, scale);
                }
            }));
            Fill("Noise", new Func<float, float, int, float>((x, z, seed) =>
            {
                lock (noises)
                {
                    if (!noises.TryGetValue(seed, out var n))
                    {
                        n = noises[seed] = new Noise(seed);
                    }
                    return n.Fbm(x, z) / 1.6f;
                }
            }));
            Fill("Print", new Action<string>(text =>
            {
                lock (ch)
                {
                    if (lines++ < 2000)
                    {
                        ch.Output.AppendLine(text);
                    }
                }
            }));
            Fill("Check", new Action(cancel.ThrowIfCancellationRequested));
            Fill("Offsets", new Func<float, float, float, float, (int, int)>(Offsets));
            Fill("MinX", snap.Ox);
            Fill("MinZ", snap.Oz);
            Fill("MaxX", snap.Ox + snap.W - 1);
            Fill("MaxZ", snap.Oz + snap.H - 1);
            Fill("Water", snap.Water);
            Fill("Seed", snap.Seed);
            Fill("World", snap.World);
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
            // Objects put on the ground: the ground as the script leaves it.
            for (int i = 0; i < ch.Add.Count; i++)
            {
                var a = ch.Add[i];
                if (float.IsNaN(a.Position.Y))
                {
                    ch.Add[i] = a with { Position = a.Position with { Y = Sample(heights, a.Position.X, a.Position.Z) } };
                }
            }
            return ch;
        }
        finally
        {
            alc.Unload();
        }
    }

    /// <summary>
    /// The whole-metre offsets from c, within reach of it, that can land within min..max: a short range of integers to
    /// count, however large reach is, and never beyond 2^24 m (where whole metres stop counting). Empty (From above
    /// To) when reach is not a size or none of them lands within min..max.
    /// </summary>
    /// <param name="c">The middle, on one axis.</param>
    /// <param name="reach">How far from it to go.</param>
    /// <param name="min">The lowest place that counts.</param>
    /// <param name="max">The highest place that counts.</param>
    internal static (int From, int To) Offsets(float c, float reach, float min, float max)
    {
        if (!(reach >= 0) || !float.IsFinite(c))
        {
            return (0, -1);
        }
        // Offsets past 2^24 m are left out: those that can still be counted are kept.
        double from = Math.Max(Math.Max(-Math.Ceiling(reach), Math.Floor(min - (double)c)), -(1 << 24));
        double to = Math.Min(Math.Min(Math.Floor(reach), Math.Ceiling(max - (double)c)), 1 << 24);
        return from <= to ? ((int)from, (int)to) : (0, -1);
    }

    /// <summary>
    /// A script's Objects.Place: the new object noted (y NaN: on the ground as the script leaves it; scale 0: the
    /// prefab's own size).
    /// </summary>
    /// <exception cref="ArgumentException">
    /// A prefab the editor cannot make, a place or turn that is not a number (NaN, infinite), a place outside the
    /// area, or a scale below 0.
    /// </exception>
    internal static void Place(Snapshot snap, Changes ch, string name, float x, float z, float yaw, float scale, float y, float pitch, float roll, bool snapIt)
    {
        int prefab = StableHash.Of(name);
        if (!snap.CanCreate(prefab))
        {
            throw new ArgumentException($"the editor cannot make a “{name}” (a prefab name, like Beech1 or rock4_forest)");
        }
        Number("x", x);
        Number("z", z);
        InArea(snap, x, z);
        if (!float.IsNaN(y))
        {
            Number("y", y);
        }
        Number("yaw", yaw);
        Number("pitch", pitch);
        Number("roll", roll);
        if (!float.IsFinite(scale) || scale < 0)
        {
            throw new ArgumentException($"a scale of {scale}: give a size above 0 (1 or 0: the prefab's own)");
        }
        ch.Add.Add((prefab, name, new Vector3(x, y, z), new Vector3(pitch, yaw, roll), scale, snapIt));
    }

    /// <summary>Throws unless a script's value is a number: not NaN, not infinite.</summary>
    /// <param name="what">The argument's name, as the script gives it.</param>
    /// <param name="v">Its value.</param>
    /// <exception cref="ArgumentException">It is NaN or infinite.</exception>
    private static void Number(string what, float v)
    {
        if (!float.IsFinite(v))
        {
            throw new ArgumentException($"{what} is {v}: give a number (not NaN or infinite)");
        }
    }

    /// <summary>
    /// Throws unless world point (x, z) is within the area: its first to its last grid point (Area.MinX to MaxX, MinZ
    /// to MaxZ), the edge included, so every point the scripts' Area.Inside takes (the edge left out) is in. A
    /// coordinate that is not a number is not.
    /// </summary>
    /// <param name="snap">The area.</param>
    /// <param name="x">World x.</param>
    /// <param name="z">World z.</param>
    /// <param name="what">The point as the error names it (null: its coordinates).</param>
    /// <exception cref="ArgumentException">It is outside the area.</exception>
    private static void InArea(Snapshot snap, float x, float z, string? what = null)
    {
        float maxX = snap.Ox + snap.W - 1, maxZ = snap.Oz + snap.H - 1;
        if (!(x >= snap.Ox && x <= maxX && z >= snap.Oz && z <= maxZ))
        {
            throw new ArgumentException($"{what ?? $"({x}, {z})"} is outside the area (x {snap.Ox} to {maxX}, z {snap.Oz} to {maxZ}): keep to Area.Inside");
        }
    }

    /// <summary>
    /// A script's Objects.Move: the thing's new place, rotation and scale noted (NaN keeps a value: as it was, or as an
    /// earlier Move of the script left it).
    /// </summary>
    /// <exception cref="ArgumentException">
    /// No such thing in the area, a place or turn given that is infinite, a place outside the area, or a scale not
    /// above 0.
    /// </exception>
    internal static void Move(Snapshot snap, Changes ch, int index, float x, float z, float y, float yaw, float pitch, float roll, float scale)
    {
        if (!snap.Things.Exists(t => t.Index == index))
        {
            throw new ArgumentException($"no object {index} in the area (Objects.All gives them)");
        }
        foreach (var (what, v) in new[] { ("x", x), ("z", z), ("y", y), ("yaw", yaw), ("pitch", pitch), ("roll", roll) })
        {
            if (!float.IsNaN(v))
            {
                Number(what, v);
            }
        }
        if (!float.IsNaN(scale) && (!float.IsFinite(scale) || scale <= 0))
        {
            throw new ArgumentException($"a scale of {scale}: give a size above 0 (1: the prefab's own)");
        }
        var t = snap.Things.Find(t => t.Index == index);
        var (p, r, sc) = ch.Moves.TryGetValue(index, out var m) ? m : (t.Position, t.Rotation, t.Scale);
        static float Or(float v, float was) => float.IsNaN(v) ? was : v;
        if (!float.IsNaN(x) || !float.IsNaN(z))
        {
            InArea(snap, Or(x, p.X), Or(z, p.Z));
        }
        ch.Moves[index] = (new Vector3(Or(x, p.X), Or(y, p.Y), Or(z, p.Z)), new Vector3(Or(pitch, r.X), Or(yaw, r.Y), Or(roll, r.Z)), Or(scale, sc));
    }

    /// <summary>
    /// The new pieces placed with snap moved to meet the snap points of the pieces standing (where the script left
    /// them; those it took away do not count) and of those placed before them, as the game's hammer does.
    /// </summary>
    /// <param name="snap">The area as the script saw it.</param>
    /// <param name="ch">The script's changes; its new pieces are moved in place.</param>
    /// <param name="cancel">Stop: checked before each piece (thousands take a while).</param>
    /// <exception cref="ArgumentException">A piece snaps to a place outside the area.</exception>
    internal static void SnapPieces(Snapshot snap, Changes ch, CancellationToken cancel = default)
    {
        if (!ch.Add.Any(a => a.Snap))
        {
            return;
        }
        var placed = new List<Hammer.Placed>();
        foreach (var t in snap.Things)
        {
            if (!ch.Remove.Contains(t.Index) && Hammer.Get(t.Prefab) != null)
            {
                var (p, r, _) = ch.Moves.TryGetValue(t.Index, out var m) ? m : (t.Position, t.Rotation, t.Scale);
                placed.Add(new(t.Index, t.Prefab, p, BlueprintFormats.FromEuler(r)));
            }
        }
        for (int i = 0; i < ch.Add.Count; i++)
        {
            cancel.ThrowIfCancellationRequested();
            var a = ch.Add[i];
            var q = BlueprintFormats.FromEuler(a.Rotation);
            if (a.Snap && Hammer.Get(a.Name) != null)
            {
                var to = Hammer.Snap(a.Name, q, a.Position, placed).Position;
                InArea(snap, to.X, to.Z, $"the {a.Name} placed at ({a.Position.X}, {a.Position.Z}) snaps to ({to.X}, {to.Z}), which");
                ch.Add[i] = a with { Position = to };
            }
            placed.Add(new(-1 - i, a.Name, ch.Add[i].Position, q));
        }
    }

    /// <summary>
    /// Where in the script an error happened ("line 12: "), when it did in the script's own code; otherwise empty.
    /// </summary>
    public static string Where(Exception ex)
    {
        if (ex is ScriptException e)
        {
            return e.Where;
        }
        var frame = new System.Diagnostics.StackTrace(ex, true).GetFrames().FirstOrDefault(f => f.GetFileName() == "script" && f.GetFileLineNumber() > 0);
        return frame != null ? $"line {frame.GetFileLineNumber()}: " : "";
    }

    /// <summary>
    /// A script's error as the output shows it: "line 12: ArgumentException: …", the exception's kind as the script
    /// threw it (ScriptException carries one from the script's process; the kind is Crash when that process died).
    /// </summary>
    public static string Describe(Exception ex) => $"{Where(ex)}{(ex is ScriptException e ? e.Kind : ex.GetType().Name)}: {ex.Message}";

    /// <summary>
    /// The script's changes into the area, one undo step. Returns how many points the game's ±8 m stopped.
    /// </summary>
    public static int Apply(EditSession s, Changes ch, string label)
    {
        int clamped = 0;
        var adds = ch.Add.Select(a => (new NewObject(0, a.Prefab, a.Position, a.Rotation, a.Scale), PieceCatalog.Get(a.Prefab)?.Tool != null)).ToList();
        // Moved things: replaced by copies where they go (those also taken away are only taken away).
        var moved = ch.Moves.Where(m => !ch.Remove.Contains(m.Key) && m.Key >= 0 && m.Key < s.Scene.Things.Count && !s.Scene.Things[m.Key].Gone).ToList();
        adds.AddRange(moved.Select(m => s.MovedCopy(m.Key, m.Value.Position, m.Value.Rotation, m.Value.Scale)));
        s.Commit(label, g =>
        {
            var touched = new List<int>();
            bool was = g.NoLimit;
            g.NoLimit = ch.NoLimit;
            try
            {
                foreach (var (p, h) in ch.Heights)
                {
                    if (g.SetHeight(p, h))
                    {
                        clamped++;
                    }
                    touched.Add(p);
                }
            }
            finally
            {
                g.NoLimit = was;
            }
            foreach (var (p, colour) in ch.Paint)
            {
                Array.Copy(colour, 0, g.Paint, p * 4, 4);
                g.PMod[p] = 1;
                touched.Add(p);
            }
            return (touched, touched.Count == 0 ? (0, 0, 0, 0) : (touched.Min(p => p % g.W) - 1, touched.Min(p => p / g.W) - 1, touched.Max(p => p % g.W) + 1, touched.Max(p => p / g.W) + 1));
        }, [.. ch.Remove, .. moved.Select(m => m.Key)], adds);
        return clamped;
    }
}
