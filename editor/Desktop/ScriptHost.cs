using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

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
        // A script without a statement (empty, or only comments) runs and changes nothing: compiled as it is, it would
        // have no entry point, a mistake in words the person never wrote.
        if (!((Microsoft.CodeAnalysis.CSharp.Syntax.CompilationUnitSyntax)user.GetRoot()).Members.OfType<Microsoft.CodeAnalysis.CSharp.Syntax.GlobalStatementSyntax>().Any())
        {
            user = Tree(code + "\nreturn;", "script");
        }
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
}
