using WorldEditor.Tests;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// A main window on its own copy of the test world (or a stand-in live game), its questions answered from a queue (no
/// when empty) and noted, what it tells noted, saves confirmed, and Auto apply and Follow off; deleted when disposed.
/// </summary>
internal sealed class WorldWindowRun : CopiedWorldWindow
{
    private static readonly Func<Formula.Env, double> Two = Formula.Compile("2", []);

    public List<string> Asked { get; } = [];
    public Queue<bool> Answers { get; } = new();
    public List<string> Told { get; } = [];
    public int Saves;
    public FakeGame? Game { get; private set; }

    /// <summary>
    /// The status bar's message.
    /// </summary>
    public string Said => W.MessageText.Text ?? "";

    public WorldWindowRun()
    {
        W.Show();
        W.Tell = text => { Told.Add(text); return Task.CompletedTask; };
        W.ConfirmSave = _ => Task.FromResult(true);
        W.Ask = (_, text, _, _) => { Asked.Add(text); return Task.FromResult(Answers.Count > 0 && Answers.Dequeue()); };
        W.SaveSettings = _ => Saves++;
        W.Settings.AutoApply = W.Settings.AreaFollow = false;
    }

    /// <summary>
    /// Opens the world and edits the area at zone (x, z), <c>size</c> zones across.
    /// </summary>
    public async Task Open(int x = 0, int z = 0, int size = 1)
    {
        await W.OpenWorld(() => Task.Run(() => WorldSession.Open(Dir)), "Opening…");
        await W.EditArea(x, z, size);
        Assert.NotNull(W.Session);
    }

    /// <summary>
    /// Opens a stand-in live game and edits the area at zone (x, z), <c>size</c> zones across.
    /// </summary>
    public async Task OpenLive(int x = 0, int z = 0, int size = 1)
    {
        var game = new FakeGame();
        Game = game;
        await StandInGameArea.OpenLive(W, game, x, z, size);
        Assert.True(W.Session!.IsLive);
    }

    /// <summary>
    /// Raises the ground 2 m in a 3 m circle at grid point (gx, gz), as one undo step with this label.
    /// </summary>
    public void Raise(float gx = 32, float gz = 32, string label = "raise") => W.Session!.Shape(gx, gz, Two, 3, 0, label);

    /// <summary>
    /// Stops the stand-in game: it answers nothing any more.
    /// </summary>
    public void StopGame()
    {
        Game?.Dispose();
        Game = null;
    }

    /// <inheritdoc/>
    public override void Dispose()
    {
        StopGame();
        base.Dispose();
    }
}
