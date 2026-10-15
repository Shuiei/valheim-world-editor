using System.Diagnostics;
using TerrainEditor.DocsScreenshots;

// The documentation's pictures (docs/images), taken in the app (Editor.cs drives it, Scenes.cs holds
// one scene per picture). The worlds are made from seeds with tools/WorldCheck (see
// docs/development.md) and never change:
//   WorldCheck create <dir>/Docs Docs yjRO99yNTI 16 --flat     the bare zone the tools are shown on
//   WorldCheck create <dir>/Fjordheim Fjordheim Fjord2026 3     a second world for the start page
// Each scene opens a fresh editor (a stand-in home: no one's names or folders) and leaves without
// saving. Run:
//   dotnet run --project tools/DocsScreenshots -- <dir> [scene ...]
// Pictures go to docs/images (or $DOCS_OUT). With scene names, only those scenes run.
// $HOMESTEAD_DLL (Homestead.dll from its Thunderstore package) puts Homestead in the stand-in game's
// BepInEx, so the start page and the Blueprints panel show it installed.
// $DOCS_APP (a released ValheimWorldEditor program) is driven instead of the Debug build in Desktop/bin.
// $DOCS_LIVE ("<bridge address> <token>", a local server running the Docs world with the plugin) is
// what the live scene connects to; without it the live scene is skipped.
if (args.Length < 1)
{
    Console.Error.WriteLine("usage: DocsScreenshots <dir> [scene ...]");
    return 2;
}
string dir = args[0];
string output = Environment.GetEnvironmentVariable("DOCS_OUT") is { Length: > 0 } o ? o : Path.Combine(Editor.Repo, "docs", "images");
var scenes = new Scenes(dir, output);
string[] names = args.Length > 1 ? args[1..] : [.. scenes.All.Select(s => s.Name)];
Directory.CreateDirectory(output);
string? homestead = Environment.GetEnvironmentVariable("HOMESTEAD_DLL") is { Length: > 0 } h ? h : null;
string? app = Environment.GetEnvironmentVariable("DOCS_APP") is { Length: > 0 } a ? a : null;
string[]? live = Environment.GetEnvironmentVariable("DOCS_LIVE") is { Length: > 0 } l ? l.Split(' ', StringSplitOptions.RemoveEmptyEntries) : null;
foreach (string name in names)
{
    var started = Stopwatch.StartNew();
    if (name == "live" && live is not { Length: 2 })
    {
        Console.WriteLine("live: skipped ($DOCS_LIVE not set)");
        continue;
    }
    // Each scene in a fresh editor: nothing carries over, nothing is saved.
    bool start = name == "start";
    using (var e = new Editor(app, gameWorld: start ? "Docs" : null, worlds: start ? [$"{dir}/Docs", $"{dir}/Fjordheim"] : null,
        homestead: homestead, recent: start ? [$"{dir}/Fjordheim", $"{dir}/Docs"] : null,
        extra: name == "live" ? ["--live", live![0], "--token", live[1]] : null))
    {
        try
        {
            var scene = scenes.All.FirstOrDefault(s => s.Name == name).Run
                ?? throw new ArgumentException($"no scene named {name}");
            scene(e);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"{name}: FAILED {ex.Message}");
            continue;
        }
    }
    Console.WriteLine($"{name}: {started.Elapsed.TotalSeconds:0} s");
}
return 0;
