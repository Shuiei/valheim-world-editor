using System.Text.Json;
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

    /// <summary>A tool's JSON answer, parsed.</summary>
    public static JsonElement J(string json) => JsonDocument.Parse(json).RootElement;

    public void Dispose()
    {
        Game?.Dispose();
        foreach (string d in new[] { Path.GetDirectoryName(Dir)!, Homestead })
        {
            try
            {
                Directory.Delete(d, true);
            }
            catch
            {
            }
        }
    }
}
