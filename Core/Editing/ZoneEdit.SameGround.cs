namespace TerrainEditor.Editing;

public sealed partial class ZoneEdit
{
    /// <summary>Same resulting ground and paint (how the height is split between level and smoothing aside).</summary>
    public bool SameGround(ZoneEdit other)
    {
        for (int i = 0; i < EditStore.Cells; i++)
        {
            float a = Modified[i] ? Level[i] + Smooth[i] : 0f, b = other.Modified[i] ? other.Level[i] + other.Smooth[i] : 0f;
            if (Math.Abs(a - b) > 1e-4f || Math.Abs(Lift[i] - other.Lift[i]) > 1e-4f || PaintModified[i] != other.PaintModified[i])
            {
                return false;
            }
            if (PaintModified[i])
            {
                for (int c = 0; c < 4; c++)
                {
                    if (Math.Abs(Paint[i * 4 + c] - other.Paint[i * 4 + c]) > 1e-4f)
                    {
                        return false;
                    }
                }
            }
        }
        return true;
    }
}
