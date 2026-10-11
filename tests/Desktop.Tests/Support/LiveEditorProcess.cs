using WorldEditor.Tests;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The editor started with --live and --token: the stand-in game's map.
/// </summary>
public sealed class LiveEditorProcess() : EditorProcess(Start())
{
    private static FakeGame? _game;
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Tests reach the game through their fixture.")]
    public FakeGame Game => _game!;

    /// <summary>
    /// The command line: a new stand-in game's address and token.
    /// </summary>
    private static string[] Start()
    {
        _game = new FakeGame();
        return ["--live", _game.Url, "--token", _game.Token];
    }

    /// <inheritdoc/>
    public override void Dispose()
    {
        base.Dispose();
        _game?.Dispose();
    }
}
