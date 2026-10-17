using System.Numerics;

namespace TerrainEditor.Desktop;

// The camera placed from outside: on a world point, by the map's search, the driver's shots and the tests.
public sealed partial class GlView
{
    /// <summary>
    /// The orbit camera on world point (x, z) at the ground, turned yaw° around it, looking down pitch°
    /// (the documentation's pictures, through the driver).
    /// height: around that height instead (inside a dungeon).
    /// </summary>
    internal void Orbit(float x, float z, float yawDegrees, float pitchDegrees, float distance, float? height = null)
    {
        if (Scene is not { } s)
        {
            return;
        }
        lock (_camLock)
        {
            var t = ToView(s, new Vector3(x, 0, z));
            t.Y = height ?? GroundOrWater(s, t);
            _target = t;
            _yaw = yawDegrees * MathF.PI / 180;
            _pitch = Math.Clamp(pitchDegrees * MathF.PI / 180, 0.05f, 1.55f);
            _distance = Math.Clamp(distance, 3, 3000);
        }
        Wake();
    }

    /// <summary>The camera turned onto a world point, close (the map's search: the object found).</summary>
    internal void Focus(Vector3 world, float distance = 35)
    {
        if (Scene is not { } s)
        {
            return;
        }
        lock (_camLock)
        {
            _target = ToView(s, world);
            _distance = distance;
        }
        Wake();
    }

    /// <summary>The camera with its target in world metres: kept when another area opens (null: no area).</summary>
    internal (float Yaw, float Pitch, float Distance, Vector3 World)? WorldCamera
    {
        get
        {
            if (Scene is not { } s)
            {
                return null;
            }
            lock (_camLock)
            {
                return (_yaw, _pitch, _distance, new Vector3(_target.X + s.Cx, _target.Y, -_target.Z + s.Cz));
            }
        }
        set
        {
            if (Scene is not { } s || value is not var (yaw, pitch, distance, world))
            {
                return;
            }
            lock (_camLock)
            {
                (_yaw, _pitch, _distance) = (yaw, pitch, distance);
                _target = ToView(s, world);
            }
            Wake();
        }
    }

    /// <summary>For tests (nothing is drawn headless): the camera the mouse is read with.</summary>
    internal void SetCamera(Vector3 eye, Vector3 target, float aspect)
    {
        _lastEye = eye;
        _lastViewProj = Matrix4x4.CreateLookAt(eye, target, Vector3.UnitY) * Perspective(60 * MathF.PI / 180, aspect, 0.5f, 6000);
    }

    /// <summary>The camera's view × projection of the last frame (or <c>SetCamera</c>).</summary>
    internal Matrix4x4 ViewProj => _lastViewProj;
}
