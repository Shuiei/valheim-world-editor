using System.Text.Json;
using System.Text.Json.Nodes;
using TerrainEditor.Save;
using WorldEditor.Tests;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// A window on a copy of the test world with its Claude tools, questions answered No, settings never saved, Auto
/// apply and area follow off, and a Homestead folder of its own; OpenLive opens a stand-in game instead.
/// </summary>
internal sealed class ClaudeRun : IDisposable
{
    public MainWindow W { get; } = new(load: false) { Width = 1600, Height = 1000 };
    public string Dir { get; } = Fixtures.CopyFixture();
    public string Homestead { get; } = Path.Combine(Path.GetTempPath(), "vwe-claude-" + Guid.NewGuid().ToString("N")[..8]);
    public FakeGame? Game { get; private set; }
    public ClaudeTools T { get; }

    public ClaudeRun()
    {
        W.Show();
        W.Tell = _ => Task.CompletedTask;
        W.Ask = (_, _, _, _) => Task.FromResult(false);
        W.SaveSettings = _ => { };
        W.Settings.AutoApply = W.Settings.AreaFollow = false;
        // The user's leave for scripts, as most tests need it; one test takes it back.
        W.Settings.ClaudeScripts = true;
        W.Blueprints.FindHomestead = () => new TerrainEditor.App.Homestead.Status(true, "1.3.2", ["test"], Homestead);
        T = new ClaudeTools(W);
    }

    /// <summary>The stand-in game opened live, and its middle zone in 3D.</summary>
    public async Task OpenLive()
    {
        Game = new FakeGame();
        var game = Game;
        await W.OpenWorld(() => WorldSession.OpenLive(new LiveBridge(game.Url, game.Token), FakeGame.Label()), "Opening…");
        W.MapPage?.Stop();
        await W.EditArea(0, 0, 1);
    }

    /// <summary>The test world opened, and the zone around (0, 0) in 3D.</summary>
    public async Task OpenArea()
    {
        await T.OpenWorld(Dir);
        await T.OpenArea(0, 0, 1);
    }

    /// <summary>A blueprint of a floor and a wall on it in the run's Homestead folder.</summary>
    public void Hut(string name, string description, params string[] tags)
    {
        static JsonNode Piece(string prefab, float x, float y) => new JsonObject
        {
            ["name"] = prefab,
            ["dx"] = x,
            ["dy"] = y,
            ["dz"] = 0,
            ["rx"] = 0,
            ["ry"] = 0,
            ["rz"] = 0,
            ["scale"] = 0,
        };
        Directory.CreateDirectory(Homestead);
        var clip = new JsonObject { ["objects"] = new JsonArray(Piece("wood_floor", 0, 0), Piece("woodwall", 0, 1)) };
        File.WriteAllText(Path.Combine(Homestead, TerrainEditor.App.Homestead.FileName(name)), TerrainEditor.App.Homestead.Write(clip, name, "Tester", null, DateTime.Now, description, tags));
    }

    /// <summary>A tool's JSON answer, parsed.</summary>
    public static JsonElement J(string json) => JsonDocument.Parse(json).RootElement;

    public void Dispose()
    {
        // Closed here, not only by the hook after the test (which waits for a test ending on Avalonia's thread), and
        // without the question about unsaved changes, which Ask answers No.
        W.CloseWithoutAsking();
        Game?.Dispose();
        foreach (string d in new[] { Path.GetDirectoryName(Dir)!, Homestead })
        {
            if (Directory.Exists(d))
            {
                Directory.Delete(d, true);
            }
                // Best effort: a folder that cannot be deleted is left for the OS to clean up.
        }
    }
}
