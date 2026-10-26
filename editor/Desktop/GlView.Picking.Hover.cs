using System.Numerics;
using Avalonia;
using Avalonia.Threading;

namespace TerrainEditor.Desktop;

// The object under the pointer: looked for once per batch of moves (Select mode), outlined faintly before a click.
public sealed partial class GlView
{
    /// <summary>
    /// The Select tool: the object under the pointer, outlined faintly before a click picks it; null when none.
    /// </summary>
    internal int? HoverObject { get; private set; }
    /// <summary>The hovered object's box lines on the graphics card.</summary>
    private uint _hoverBoxVao, _hoverBoxVbo;

    /// <summary>A look for the object under the pointer is waiting (<see cref="PickHover"/>).</summary>
    private bool _hoverQueued;

    /// <summary>Tests: how many times the object under the pointer was looked for.</summary>
    internal int HoverPicks { get; private set; }

    /// <summary>Queues a look for the object under the pointer, unless one is waiting.</summary>
    private void QueueHoverPick()
    {
        if (!_hoverQueued)
        {
            _hoverQueued = true;
            Dispatcher.UIThread.Post(PickHover, DispatcherPriority.Background);
        }
    }

    /// <summary>
    /// The object under the pointer as it is now, looked for once for all the pointer's moves since the last look
    /// (Select mode, no button down: as the move that queued it would have).
    /// </summary>
    private void PickHover()
    {
        _hoverQueued = false;
        if (!SelectMode || Tool != null || _selectDown || _pointer is not Point p)
        {
            return;
        }
        HoverPicks++;
        SetHoverObject(SelectTool.OverHandle ? null : ObjectAt(p, _surfaceSize));
    }

    /// <summary>The object under the pointer's box, faint, when a click can pick it.</summary>
    private void DrawHoverObject(Matrix4x4 vp)
    {
        // Only what a click can pick: drawn and not deleted (a deleted object stays under the pointer).
        if (!SelectMode || Tool != null || HoverObject is not int i || i >= _bounds.Length || _selection.Contains(i)
            || !_known[i] || !_shown[(int)_kinds[i]] || Scene is not { } sc || i >= sc.Things.Count || sc.Things[i].Gone)
        {
            return;
        }
        DrawLines(ref _hoverBoxVao, ref _hoverBoxVbo, SelectionBoxes([OutlineBoxOf(sc, i)]), vp, new Vector4(1, 0.85f, 0.55f, 0.6f), blend: true, width: SelectionWidth);
    }
}
