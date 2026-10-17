using TerrainEditor.Editing;

namespace TerrainEditor.Desktop;

public sealed partial class Ground
{
    /// <summary>Starts over from the edits (when loaded, and after saving).</summary>
    public void TakeEdits(EditStore edits)
    {
        _own.Clear();
        for (int zz = 0; zz < ZonesZ; zz++)
        {
            for (int zx = 0; zx < ZonesX; zx++)
            {
                var e = edits.Get(X0 + zx, Z0 + zz);
                _own[(zx, zz)] = e;
                foreach (var (i, p) in ZoneGridPoints(zz, zx))
                {
                    Mod[p] = (byte)(e != null && e.Modified[i] ? 1 : 0);
                    Level[p] = e?.Level[i] ?? 0;
                    Smooth[p] = e?.Smooth[i] ?? 0;
                    PMod[p] = (byte)(e != null && e.PaintModified[i] ? 1 : 0);
                    Lift[p] = e?.Lift[i] ?? 0;
                    for (int c = 0; c < 4; c++)
                    {
                        Paint[p * 4 + c] = e?.Paint[i * 4 + c] ?? 0;
                    }
                }
            }
        }
        _loaded = Snapshot();
    }
}
