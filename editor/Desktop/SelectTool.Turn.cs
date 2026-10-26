
namespace TerrainEditor.Desktop;

public sealed partial class SelectTool
{
    /// <summary>Turns the selection about its middle (positive: clockwise from above), committed shortly after.</summary>
    public void Turn(float degrees)
    {
        if (Begin() is not { } m)
        {
            return;
        }
        m.Turn = ((m.Turn + degrees) % 360 + 360) % 360;
        if (m.Turn > 180)
        {
            m.Turn -= 360;
        }
        Preview(m);
        Later();
    }

    /// <summary>Lifts (or, negative, lowers) the selection, committed shortly after.</summary>
    public void Lift(float metres)
    {
        if (Begin() is not { } m)
        {
            return;
        }
        m.Dy += metres;
        Preview(m);
        Later();
    }
}
