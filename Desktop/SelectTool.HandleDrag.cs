using System.Numerics;

namespace TerrainEditor.Desktop;

public sealed partial class SelectTool
{
    /// <summary>The ring drag's turn: the heading under the pointer, in 15° steps with Ctrl, wrapped to -180..180.</summary>
    /// <returns>False when the pointer has no heading, and nothing changed.</returns>
    private static bool TurnBy(Move mv, (Gizmo.Handle Handle, Vector3 Centre, float Start, float Dx, float Dz, float Dy, float Turn) hd, Vector3 ro, Vector3 rd, bool ctrl)
    {
        if (Gizmo.Heading(ro, rd, hd.Centre) is not float h)
        {
            return false;
        }
        // Ctrl: 15° steps.
        float turn = hd.Turn + h - hd.Start;
        if (ctrl)
        {
            turn = MathF.Round(turn / 15) * 15;
        }
        mv.Turn = ((turn + 180) % 360 + 360) % 360 - 180;
        return true;
    }

    /// <summary>
    /// The axis drag's slide: how far along the handle's axis the pointer is from where the drag began, in half-metre
    /// steps with Ctrl. X and Z keep following the ground.
    /// </summary>
    /// <returns>False when the pointer has no place along the axis, and nothing changed.</returns>
    private static bool SlideBy(Move mv, (Gizmo.Handle Handle, Vector3 Centre, float Start, float Dx, float Dz, float Dy, float Turn) hd, Vector3 ro, Vector3 rd, bool ctrl)
    {
        if (Gizmo.Along(ro, rd, hd.Centre, Gizmo.Axis(hd.Handle)) is not float t)
        {
            return false;
        }
        // Ctrl: half-metre steps. X and Z keep following the ground.
        float by = t - hd.Start;
        if (ctrl)
        {
            by = MathF.Round(by / 0.5f) * 0.5f;
        }
        (mv.Dx, mv.Dz, mv.Dy) = (hd.Dx, hd.Dz, hd.Dy);
        switch (hd.Handle)
        {
            case Gizmo.Handle.X: mv.Dx += by; break;
            case Gizmo.Handle.Z: mv.Dz += by; break;
            default: mv.Dy += by; break;
        }
        return true;
    }
}
