using Avalonia.OpenGL.Controls;
using Avalonia.Threading;
using TerrainEditor.App;
using TerrainEditor.Terrain;

namespace TerrainEditor.Desktop;

/// <summary>
/// The world's player-built pieces and ruins, read in the background and kept for the map's buildings.
/// </summary>
public sealed partial class MapView : OpenGlControlBase, IDisposable
{
    /// <summary>Counts the pieces' reads started: only the latest one's result is shown.</summary>
    private long _piecesTurn;

    /// <summary>
    /// Reads the world's player-built pieces in the background (deleted ones left out); dropped if another world is
    /// shown or another read started meanwhile; a failure is logged.
    /// </summary>
    private void ReadPieces()
    {
        var s = _session;
        if (s == null)
        {
            return;
        }
        var world = s.World;
        var deleted = s.Edits.Deleted;
        // Two reads can run at once (a refresh while one runs): only the latest is shown, else an older one finishing
        // last would bring back pieces deleted since.
        long turn = ++_piecesTurn;
        Task.Run(() =>
        {
            float[] data, ruins;
            bool[] joined;
            (bool Ruin, int Index)[] order;
            try
            {
                data = PieceCatalog.Encode(world, deleted);
                (ruins, joined) = PieceCatalog.EncodeRuins(world, deleted);
                const int S = PieceCatalog.Stride;
                // Lower pieces first so roofs end up on top, like looking down from above.
                order = [.. Enumerable.Range(0, data.Length / S).Select(i => (Ruin: false, Index: i))
                    .Concat(Enumerable.Range(0, ruins.Length / S).Select(i => (Ruin: true, Index: i)))
                    .OrderBy(e => (e.Ruin ? ruins : data)[e.Index * S + 8])];
            }
            catch (Exception ex)
            {
                // Logged: the map shows the buildings it had (none on a first read).
                Log.Caught(ex, "reading the buildings shown on the map");
                return;
            }
            Dispatcher.UIThread.Post(() =>
            {
                if (_session != s || _piecesTurn != turn)
                {
                    return;
                }
                _pieceOrder = order;
                _ruins = ruins;
                _ruinJoined = joined;
                _pieces = data;
                _buildingsKey = default;
                PiecesRead?.Invoke();
                RequestNextFrameRendering();
            });
        });
    }
}
