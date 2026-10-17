
namespace TerrainEditor.Desktop;

public sealed partial class Ground
{
    /// <summary>True when nothing at the point (height, paint, lift) changed since loading or the last save.</summary>
    private bool UnchangedSinceLoad(int g)
    {
        if (_loaded is not { } loaded)
        {
            // As made: everything 0.
            return Level[g] == 0 && Smooth[g] == 0 && Mod[g] == 0 && PMod[g] == 0 && Lift[g] == 0
                && Paint[g * 4] == 0 && Paint[g * 4 + 1] == 0 && Paint[g * 4 + 2] == 0 && Paint[g * 4 + 3] == 0;
        }
        if (Level[g] != loaded.Level[g] || Smooth[g] != loaded.Smooth[g] || Mod[g] != loaded.Mod[g] || PMod[g] != loaded.PMod[g] || Lift[g] != loaded.Lift[g])
        {
            return false;
        }
        for (int c = 0; c < 4; c++)
        {
            if (Paint[g * 4 + c] != loaded.Paint[g * 4 + c])
            {
                return false;
            }
        }
        return true;
    }
}
