using System.Numerics;

namespace TerrainEditor.Desktop;

// The camera of a frame: the orbit eye round its target, or the fly eye where it is.
public sealed partial class GlView
{
    /// <summary>
    /// The camera for the frame: the orbit's eye looks at the target from its distance, the fly eye looks forward
    /// from where it is. Sets the game look's fog start to the same distance.
    /// </summary>
    /// <returns>The view matrix and the eye's position.</returns>
    internal (Matrix4x4 View, Vector3 Eye) CameraView()
    {
        lock (_camLock)
        {
            if (_eye == EyeMode.Orbit)
            {
                var orbitEye = _target + _distance * new Vector3(MathF.Cos(_pitch) * MathF.Sin(_yaw), MathF.Sin(_pitch), MathF.Cos(_pitch) * MathF.Cos(_yaw));
                GameLookGl.FogStart = _distance;
                return (Matrix4x4.CreateLookAt(orbitEye, _target, Vector3.UnitY), orbitEye);
            }
            GameLookGl.FogStart = 0;
            var flyEye = _eyePos;
            return (Matrix4x4.CreateLookAt(flyEye, flyEye + Forward, Vector3.UnitY), flyEye);
        }
    }
}
