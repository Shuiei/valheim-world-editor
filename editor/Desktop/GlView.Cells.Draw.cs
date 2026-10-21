using Silk.NET.OpenGL;

namespace TerrainEditor.Desktop;

// Drawing a batch's instances in the cells in view: the runs drawn whole, or a few at a time.
public sealed partial class GlView
{
    /// <summary>
    /// Draws the runs of a batch's instances (its vertex array bound) whose cell shows (<see cref="Shows"/>), each
    /// row of neighbouring runs at once, or all of them at once when they are few and any shows. OpenGL ES 3.0 and
    /// OpenGL 4.0 cannot start at an instance, so the instance attributes are pointed at the row's first one. Returns
    /// how many instances were drawn.
    /// </summary>
    /// <param name="b">The batch.</param>
    /// <param name="vbo">The buffer of the instances: the batch's own, or another one (pointed at anew).</param>
    /// <param name="instances">How many instances it holds.</param>
    /// <param name="runs">The instances' runs by cell (see <see cref="Runs"/>).</param>
    /// <param name="reach">How far from the eye a cell's instances are drawn.</param>
    /// <param name="calls">The draw calls made.</param>
    private unsafe int DrawRuns(Batch b, uint vbo, int instances, (int Cell, int Start, int Count)[] runs, float reach, out int calls)
    {
        bool own = vbo == b.InstanceVbo;
        int pointed = own ? b.PointedAt : -1;
        int start = -1, count = 0, drawn = 0, made = 0;
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, vbo);
        void Flush()
        {
            if (count == 0)
            {
                return;
            }
            if (pointed != start)
            {
                PointInstances(start * 64L);
                pointed = start;
            }
            _gl.DrawElementsInstanced(PrimitiveType.Triangles, (uint)b.IndexCount, DrawElementsType.UnsignedInt, (void*)0, (uint)count);
            made++;
            drawn += count;
            count = 0;
        }
        if (instances <= SmallBatch || runs.Length == 1)
        {
            if (runs.Any(r => Shows(r.Cell, reach)))
            {
                start = 0;
                count = instances;
                Flush();
            }
        }
        else
        {
            foreach (var (cell, first, n) in runs)
            {
                if (!Shows(cell, reach))
                {
                    Flush();
                    continue;
                }
                if (count > 0 && start + count == first)
                {
                    count += n;
                }
                else
                {
                    Flush();
                    start = first;
                    count = n;
                }
            }
            Flush();
        }
        if (own)
        {
            b.PointedAt = pointed;
        }
        calls = made;
        return drawn;
    }
}
