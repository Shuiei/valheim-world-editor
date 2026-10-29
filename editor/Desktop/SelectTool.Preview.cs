using System.Numerics;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class SelectTool
{
    /// <summary>Shows the move in progress in the view (as previews) and says how far it went.</summary>
    /// <param name="m">The move in progress.</param>
    private void Preview(Move m)
    {
        GroundBlock(m);
        Snap(m);
        var shown = new Dictionary<int, WorldScene.Thing>();
        foreach (var it in m.Items)
        {
            shown[it.Index] = it.Thing with { Position = Target(m, it), Rotation = it.Thing.Rotation + new Vector3(0, m.Turn, 0) };
        }
        _view.SetPreviews(shown);
        float moved = MathF.Sqrt(m.Dx * m.Dx + m.Dz * m.Dz);
        Message?.Invoke(Said.Of("select.msg.moving", m.Items.Count, WholeGroups(m.Groups, m.Noun, deleted: false), moved,
            MathF.Abs(m.Turn) >= NoTurn ? Said.Of("select.msg.turned", m.Turn) : Said.Empty,
            MathF.Abs(m.Dy) >= NoLift ? Said.Of("select.msg.lifted", m.Dy) : Said.Empty,
            m.Snapped != null ? Said.Of("select.msg.snapped", m.Snapped) : Said.Empty), false);
    }

    /// <summary>Puts a move back (Esc); false when there was none.</summary>
    public bool Cancel()
    {
        if (_move == null)
        {
            return false;
        }
        _commitTimer.Stop();
        _move = null;
        _handleDrag = null;
        _view.SetPreviews(null);
        Message?.Invoke(Said.Of("select.msg.cancelled"), false);
        return true;
    }

    /// <summary>Restarts the commit timer: the move is committed once the keys stop.</summary>
    private void Later()
    {
        _commitTimer.Stop();
        _commitTimer.Start();
    }
}
