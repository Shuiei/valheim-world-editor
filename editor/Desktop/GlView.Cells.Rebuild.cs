using System.Numerics;
using Silk.NET.OpenGL;

namespace TerrainEditor.Desktop;

// A group's instances made from where its things are now, sorted by cell into runs.
public sealed partial class GlView
{
    /// <summary>A group's instances (and its things' boxes) from where its things are now.</summary>
    private unsafe void Rebuild(WorldScene s, Group g)
    {
        EnsureCells(s);
        var list = new List<(int Cell, Matrix4x4 M, Vector3 Tint)>();
        var support = _support;
        var previews = _previews;
        var hidden = _hidden;
        // The largest scale an instance is drawn at (its own, or the model's when it has none): culling reaches as
        // far as the largest one needs.
        float rootMost = MathF.Max(g.RootScale.X, MathF.Max(g.RootScale.Y, g.RootScale.Z)), most = 0;
        lock (s.Things)
        {
            lock (_objLock)
            {
                foreach (int i in g.Things)
                {
                    var t = previews.TryGetValue(i, out var p) ? p : s.Things[i];
                    // A hidden object is drawn and picked as a gone one (GlView.Hide).
                    bool gone = t.Gone || hidden.Contains(i);
                    var m = Placement(s, t, g.RootScale);
                    // Without its model's box yet: a metre around it (the stand-in box).
                    var bounds = PlacedBox(g.Box, m);
                    if (g.Box != null)
                    {
                        _bounds[i] = bounds;
                        _boundsAt[i] = t.Position;
                    }
                    bool known = !gone && g.Box != null;
                    // A box known for the first time: the selection's outline (which left it out) is made again.
                    _selectionDirty |= known && !_known[i];
                    _known[i] = known;
                    if (!gone)
                    {
                        int cell = CellOf(s, m.Translation);
                        Grow(_cellBounds, cell, bounds);
                        list.Add((cell, m, support != null && support.TryGetValue(i, out float sv) ? SupportTint(sv) : Vector3.Zero));
                        most = MathF.Max(most, t.Scale > 0 ? t.Scale : rootMost);
                    }
                }
            }
        }
        // By cell, so each cell's instances are one run of the buffer.
        list.Sort((a, b) => a.Cell.CompareTo(b.Cell));
        var cells = Runs([.. list.Select(e => e.Cell)]);
        float size = g.Box is { } gb ? Vector3.Distance(gb.Min, gb.Max) * (list.Count > 0 ? most : rootMost) : 1;
        var data = new Matrix4x4[list.Count];
        foreach (var (b, pre) in g.Batches)
        {
            for (int k = 0; k < list.Count; k++)
            {
                var w = pre * list[k].M;
                (w.M14, w.M24, w.M34) = (list[k].Tint.X, list[k].Tint.Y, list[k].Tint.Z);
                data[k] = w;
            }
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, b.InstanceVbo);
            fixed (Matrix4x4* ptr = data)
            {
                _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(data.Length * 64), ptr, BufferUsageARB.DynamicDraw);
            }
            b.Instances = data.Length;
            b.Cells = [.. cells];
            b.Size = size;
        }
    }

    /// <summary>
    /// Instances' runs by cell, in their order: each run the instances one after another in the same cell (one run per
    /// cell when they are sorted by cell).
    /// </summary>
    /// <param name="cells">Each instance's cell, in the order of the instances.</param>
    internal static (int Cell, int Start, int Count)[] Runs(IReadOnlyList<int> cells)
    {
        var runs = new List<(int Cell, int Start, int Count)>();
        for (int k = 0; k < cells.Count; k++)
        {
            if (runs.Count > 0 && runs[^1].Cell == cells[k])
            {
                runs[^1] = runs[^1] with { Count = runs[^1].Count + 1 };
            }
            else
            {
                runs.Add((cells[k], k, 1));
            }
        }
        return [.. runs];
    }
}
