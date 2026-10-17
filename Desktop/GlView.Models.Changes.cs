namespace TerrainEditor.Desktop;

// Changes to the things: they appeared, went or came back (their groups are drawn again), and the previews shown
// for a move in progress.
public sealed partial class GlView
{
    /// <summary>Things appeared, went or came back (EditSession): their groups are drawn again.</summary>
    private void OnThingsChanged(IReadOnlyList<int> indices)
    {
        var s = Scene;
        if (s == null)
        {
            return;
        }
        _shapesDirty = true;
        lock (_groups)
        {
            lock (s.Things)
            {
                foreach (int i in indices)
                {
                    MarkDirty(GroupOf(s, i));
                }
            }
        }
        lock (_selection)
        {
            _selection.RemoveWhere(i => s.Things[i].Gone);
        }
        _selectionDirty = true;
        _overlaysDirty = true;
        _marksDirty = true;
        _spawnsDirty = true;
        SendKindCounts(s);
        Wake();
    }

    /// <summary>Shows things somewhere else than where they are (a move in progress), or (null) where they are.</summary>
    internal void SetPreviews(Dictionary<int, WorldScene.Thing>? previews)
    {
        var s = Scene;
        if (s == null)
        {
            return;
        }
        var old = _previews;
        _previews = previews ?? [];
        lock (_groups)
        {
            foreach (int i in old.Keys.Concat(_previews.Keys))
            {
                var t = s.Things[i];
                if (_groups.TryGetValue(KeyOf(t), out var g))
                {
                    MarkDirty(g);
                }
            }
        }
        _selectionDirty = true;
        Wake();
    }
}
