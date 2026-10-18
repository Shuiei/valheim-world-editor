using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class MainWindow
{
    /// <summary>
    /// Said in the status bar: what came from the game that shows (objects come, go or move; ground). A
    /// change of data alone (a fire burning down, a chest used) is taken in without a word.
    /// </summary>
    private void SayFromGame(WorldSession.Followed f)
    {
        var parts = new List<Said>();
        int added = f.Merged.Added.Count, gone = f.Merged.Vanished.Count + f.Ours.Count;
        if (added > 0)
        {
            parts.Add(Said.Of("live.fromGame.added", added));
        }

        if (gone > 0)
        {
            parts.Add(Said.Of("live.fromGame.removed", gone));
        }

        if (f.Ground.Count > 0)
        {
            parts.Add(Said.Of("live.fromGame.ground", f.Ground.Count));
        }

        var text = parts.Count > 0 ? Said.Of("live.msg.fromGame", Said.Join(", ", parts)) : Said.Empty;
        if (f.Kept.Count > 0)
        {
            text += Said.Of("live.msg.groundKept", string.Join("; ", f.Kept.Select(z => $"{z.X}, {z.Z}")));
        }
        if (text.English != "")
        {
            Msg(text);
        }
    }

    /// <summary>
    /// Asked by Apply live before writing over ground the game changed where the editor has changes not
    /// applied yet (whatever was dug or built there since). True to go on.
    /// </summary>
    internal async Task<bool> ConfirmOverGame(IReadOnlyList<(int X, int Z)> over) =>
        await Ask(Lang.T("live.ask.overGame.title"),
            Lang.T("live.ask.overGame.text", over.Count, string.Join("; ", over.Take(6).Select(z => $"{z.X}, {z.Z}")) + (over.Count > 6 ? "…" : "")),
            Lang.T("live.ask.overGame.yes"), Lang.T("live.ask.overGame.no"));
}
