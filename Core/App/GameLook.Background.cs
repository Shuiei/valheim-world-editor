// The background readings GameLook.Check starts: the game data of an indexed game, and the index plus the data of a new one.

namespace TerrainEditor.App;

public static partial class GameLook
{
    /// <summary>
    /// Makes the game data ready for an indexed game, in the background: "ready" once it is in use.
    /// </summary>
    /// <param name="game">The indexed reader.</param>
    private static void CheckGameData(GameBundles game)
    {
        string state;
        string? message;
        try
        {
            (state, message) = GameData.Ensure(game) ? ("ready", null) : ("failed", DataUnread);
        }
        catch (Exception e)
        {
            // Whatever fails, the state leaves "running".
            Log.Caught(e, "making the game's data ready");
            (state, message) = ("failed", "The game's data could not be read: " + e.Message);
        }
        FinishReading(game, state, message);
    }

    /// <summary>
    /// Indexes the game's bundles, then makes the game data ready, in the background, telling the reading's progress.
    /// </summary>
    /// <param name="game">The reader to index.</param>
    private static void IndexBundles(GameBundles game)
    {
        string state, message;
        // The bar: the bundles' index first (a tenth: its table of contents only), then the game data.
        void Report(double progress, string step) => _told = new Told(game, progress, step);
        try
        {
            game.EnsureIndexed((done, count) => Report(0.1 * done / Math.Max(1, count), $"Indexing the game's bundles: {done} of {count}"));
            int n = game.Index.Count;
            (state, message) = n == 0 ? ("failed", "No models were found in the game's files.")
                : !GameData.Ensure(game, (p, step) => Report(0.1 + (0.9 * p), step)) ? ("failed", DataUnread)
                : ("ready", "The game's look is ready.");
        }
        catch (Exception e)
        {
            // Whatever fails, the state leaves "running": the start page would wait for ever.
            Log.Caught(e, "indexing the game's bundles and data");
            (state, message) = ("failed", "The game's files could not be read: " + e.Message);
        }
        FinishReading(game, state, message);
    }
}
