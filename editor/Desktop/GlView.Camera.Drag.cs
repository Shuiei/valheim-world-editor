using System.Numerics;
using Avalonia;
using Avalonia.Input;

namespace TerrainEditor.Desktop;

// The mouse drags on the view: sliding the ground, or turning the camera.
public sealed partial class GlView
{
    /// <summary>Slides the view along the ground by a drag of (dx, dy) points (under the camera lock).</summary>
    private void Slide(float dx, float dy)
    {
        float k = _distance * 0.0015f, c = MathF.Cos(_yaw), sn = MathF.Sin(_yaw);
        _target += new Vector3(-dx * c - dy * sn, 0, dx * sn - dy * c) * k;
        FollowGround();
    }

    /// <summary>
    /// A mouse drag: slides the view (Panning; in Orbit also the left or middle button) or turns it (right
    /// button).
    /// </summary>
    private void Drag(Point p)
    {
        if (_dragFrom is Point from)
        {
            float dx = (float)(p.X - from.X), dy = (float)(p.Y - from.Y);
            lock (_camLock)
            {
                if (Panning)
                {
                    Slide(dx, dy);
                }
                else if (_dragButton == PointerUpdateKind.RightButtonPressed)
                {
                    _yaw -= dx * 0.005f;
                    _pitch = _eye == EyeMode.Orbit ? Math.Clamp(_pitch + dy * 0.005f, 0.05f, 1.55f) : Math.Clamp(_pitch + dy * 0.005f, -1.45f, 1.45f);
                }
                else if (_eye == EyeMode.Orbit && _dragButton is PointerUpdateKind.MiddleButtonPressed or PointerUpdateKind.LeftButtonPressed)
                {
                    Slide(dx, dy);
                }
            }
            _dragFrom = p;
        }
        Wake();
    }
}
