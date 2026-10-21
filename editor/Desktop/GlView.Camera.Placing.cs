using System.Numerics;
using Avalonia.Threading;

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

    /// <summary>
    /// The camera as the editor starts with it: turning around its target, at the start's turn, tilt and distance (the
    /// driver's tests, which share one editor, each start from it). The target stays: each area sets its own.
    /// </summary>
    internal void ResetCamera()
    {
        bool eyeChanged;
        lock (_camLock)
        {
            eyeChanged = _eye != EyeMode.Orbit;
            _eye = EyeMode.Orbit;
            (_yaw, _pitch, _distance) = (StartYaw, StartPitch, StartDistance);
        }
        if (eyeChanged)
        {
            Dispatcher.UIThread.Post(() => EyeChanged?.Invoke(EyeMode.Orbit));
        }
        Wake();
    }

    /// <summary>For tests (nothing is drawn headless): the camera the mouse is read with.</summary>
    internal void SetCamera(Vector3 eye, Vector3 target, float aspect)
    {
        _lastEye = eye;
        _lastViewProj = Matrix4x4.CreateLookAt(eye, target, Vector3.UnitY) * Perspective(60 * MathF.PI / 180, aspect, 0.5f, 6000);
    }

    /// <summary>The camera's view × projection of the last frame (or <c>SetCamera</c>).</summary>
    internal Matrix4x4 ViewProj => _lastViewProj;

    /// <summary>
    /// The camera in words, in world metres, for the driver's errors: how it moves, its target, turn, tilt and
    /// distance now, where the eye was at the last frame (what the mouse is read with) and when that frame ended.
    /// </summary>
    internal string CameraSays()
    {
        if (Scene is not { } s)
        {
            return "no area";
        }
        long last = Interlocked.Read(ref _lastFrameAt);
        string frame = last < 0 ? "no frame drawn yet" : $"the last frame ended {_clock.ElapsedMilliseconds - last} ms ago";
        lock (_camLock)
        {
            return FormattableString.Invariant(
                $"{_eye}, target {_target.X + s.Cx:0.#} {_target.Y:0.#} {-_target.Z + s.Cz:0.#}, yaw {_yaw * 180 / MathF.PI:0.#}°, pitch {_pitch * 180 / MathF.PI:0.#}°, distance {_distance:0.#} m; eye at the last frame {_lastEye.X + s.Cx:0.#} {_lastEye.Y:0.#} {-_lastEye.Z + s.Cz:0.#}; {frame}");
        }
    }
}
