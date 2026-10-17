using System.Numerics;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class SelectTool
{
    /// <summary>
    /// Every building piece connected to the selected ones through pieces that touch (boxes grown a little), added
    /// to the selection.
    /// </summary>
    /// <param name="from">The pieces to start from; null for the selection.</param>
    public void WholeBuilding(IEnumerable<int>? from = null)
    {
        var s = Scene;
        if (s == null)
        {
            return;
        }
        var start = (from ?? _view.Selected).Where(i => IsPiece(s.Things[i])).ToList();
        if (start.Count == 0)
        {
            Message?.Invoke(Said.Of("select.msg.buildingFirst"), true);
            return;
        }
        var boxes = new Dictionary<int, (Vector3 Min, Vector3 Max)>();
        foreach (int i in Shown().Where(i => IsPiece(s.Things[i])))
        {
            var (lo, hi) = _view.BoundsOf(i);
            boxes[i] = (lo - new Vector3(0.15f), hi + new Vector3(0.15f));
        }
        const float Cell = 4;
        var grid = new Dictionary<(int, int, int), List<int>>();
        static IEnumerable<(int, int, int)> Cells((Vector3 Min, Vector3 Max) b)
        {
            for (int x = (int)MathF.Floor(b.Min.X / Cell); x <= (int)MathF.Floor(b.Max.X / Cell); x++)
            {
                for (int y = (int)MathF.Floor(b.Min.Y / Cell); y <= (int)MathF.Floor(b.Max.Y / Cell); y++)
                {
                    for (int z = (int)MathF.Floor(b.Min.Z / Cell); z <= (int)MathF.Floor(b.Max.Z / Cell); z++)
                    {
                        yield return (x, y, z);
                    }
                }
            }
        }
        foreach (var (i, b) in boxes)
        {
            foreach (var c in Cells(b))
            {
                (grid.TryGetValue(c, out var l) ? l : grid[c] = []).Add(i);
            }
        }
        static bool Touch((Vector3 Min, Vector3 Max) a, (Vector3 Min, Vector3 Max) b) =>
            a.Min.X <= b.Max.X && a.Max.X >= b.Min.X && a.Min.Y <= b.Max.Y && a.Max.Y >= b.Min.Y && a.Min.Z <= b.Max.Z && a.Max.Z >= b.Min.Z;
        var seen = start.Where(boxes.ContainsKey).ToHashSet();
        var queue = new Stack<int>(seen);
        while (queue.Count > 0)
        {
            var b = boxes[queue.Pop()];
            // Each of a box's cells is in the grid: the box itself was put in every one.
            foreach (var c in Cells(b))
            {
                foreach (int o in grid[c])
                {
                    if (!seen.Contains(o) && Touch(boxes[o], b))
                    {
                        seen.Add(o);
                        queue.Push(o);
                    }
                }
            }
        }
        _view.Select(seen, add: true);
        Message?.Invoke(Said.Of("select.msg.building", seen.Count), false);
    }
}
