using System.Numerics;
using Avalonia;
using Avalonia.Input;
using Avalonia.Threading;

namespace TerrainEditor.Desktop;

// The camera: the orbit around a target point, Walk and Fly (F), moving with the keys and the mouse's drags,
// kept over the ground, and set from outside (the window, the driver, the tests).
public sealed partial class GlView
{
    // ---- Camera: orbit around a target point (view space), like the web editor's.
    /// <summary>Guards the camera's values, which the drawing thread and the UI thread both use.</summary>
    private readonly Lock _camLock = new();
    /// <summary>The point the orbit camera turns around, in view space.</summary>
    private Vector3 _target;
    /// <summary>The orbit camera's distance (metres), turn and tilt (radians).</summary>
    private float _distance = 140, _yaw = 0.8f, _pitch = 0.9f;
    /// <summary>Walk and Fly (F): the camera itself at _eyePos, looking along yaw and pitch (positive: down).</summary>
    public enum EyeMode
    {
        /// <summary>Turning around a target point.</summary>
        Orbit,
        /// <summary>On the ground, 6 m/s (Shift: 12).</summary>
        Walk,
        /// <summary>Free in the air, 15 m/s (Shift: 45), kept above the ground.</summary>
        Fly,
    }
    /// <summary>How the camera moves now.</summary>
    private EyeMode _eye = EyeMode.Orbit;
    /// <summary>Walk and Fly: where the camera is, in view space.</summary>
    private Vector3 _eyePos;
    /// <summary>How the camera moves now (F changes it).</summary>
    public EyeMode Eye { get { lock (_camLock) { return _eye; } } }
    /// <summary>Raised on the UI thread when F changes how the camera moves.</summary>
    public event Action<EyeMode>? EyeChanged;
    /// <summary>A player's eye height over the ground, in metres.</summary>
    private const float EyeHeight = 1.8f;
    /// <summary>Walk and Fly: the way the camera looks, from yaw and pitch.</summary>
    private Vector3 Forward => new(-MathF.Sin(_yaw) * MathF.Cos(_pitch), -MathF.Sin(_pitch), -MathF.Cos(_yaw) * MathF.Cos(_pitch));

    /// <summary>F: the usual view, then walking at a player's eye height, then flying, then back.</summary>
    internal void CycleEye()
    {
        lock (_camLock)
        {
            switch (_eye)
            {
                case EyeMode.Orbit:
                    _eye = EyeMode.Walk;
                    _eyePos = _target;
                    _pitch = 0.05f;
                    KeepOverGround(walk: true);
                    break;
                case EyeMode.Walk:
                    _eye = EyeMode.Fly;
                    break;
                default:
                    // Back around the point ahead, on the ground.
                    _eye = EyeMode.Orbit;
                    var ahead = _eyePos + new Vector3(-MathF.Sin(_yaw), 0, -MathF.Cos(_yaw)) * 20;
                    _target = ahead;
                    FollowGround();
                    _pitch = 0.9f;
                    _distance = Math.Max(20, Vector3.Distance(_eyePos, _target));
                    break;
            }
        }
        var e = Eye;
        Dispatcher.UIThread.Post(() => EyeChanged?.Invoke(e));
        Wake();
    }

    /// <summary>
    /// Above this the game takes a place for indoors (Character.InInterior): dungeons are built there,
    /// 5000 m over their entrances. The camera does not keep to the ground up there.
    /// </summary>
    internal const float InteriorHeight = 3000f;

    /// <summary>
    /// Walking: the eyes 1.8 m over the ground (or the water); flying: never below that. Not in a
    /// dungeon (the ground is far below).
    /// </summary>
    private void KeepOverGround(bool walk)
    {
        var s = Scene;
        if (s == null || _eyePos.Y > InteriorHeight)
        {
            return;
        }
        float floor = Math.Max(Picking.HeightAt(s, _eyePos.X, _eyePos.Z), s.Water) + EyeHeight;
        _eyePos.Y = walk ? floor : Math.Max(_eyePos.Y, floor);
    }
    /// <summary>The camera of the last frame, to tell whether it moved.</summary>
    private Matrix4x4 _lastView;

    /// <summary>For tests: the camera (orbit: turn, tilt, distance and target).</summary>
    internal (float Yaw, float Pitch, float Distance, Vector3 Target) Camera { get { lock (_camLock) { return (_yaw, _pitch, _distance, _target); } } }
    /// <summary>For tests: where the camera is in Walk and Fly.</summary>
    internal Vector3 EyePosition { get { lock (_camLock) { return _eyePos; } } }
    /// <summary>For tests: move as if the keys were held for dt seconds.</summary>
    internal bool Step(float dt) => MoveWithKeys(dt);

    /// <summary>OpenGL's perspective (depth from -1 to 1), as a System.Numerics (row vector) matrix.</summary>
    internal static Matrix4x4 Perspective(float fovY, float aspect, float near, float far)
    {
        float f = 1 / MathF.Tan(fovY / 2);
        return new Matrix4x4(
            f / aspect, 0, 0, 0,
            0, f, 0, 0,
            0, 0, (far + near) / (near - far), -1,
            0, 0, 2 * far * near / (near - far), 0);
    }

    // ---------------------------------------------------------------- camera input
    /// <summary>
    /// Moves the camera by the keys held (WASD or arrows; Space and C up and down when flying; Shift faster when
    /// walking or flying) for <paramref name="dt"/> seconds (at most 0.1). True when it moved.
    /// </summary>
    private bool MoveWithKeys(float dt)
    {
        Vector3 d = Vector3.Zero;
        bool fast;
        float up = 0;
        lock (_keys)
        {
            if (_keys.Contains(Key.W) || _keys.Contains(Key.Up))
            {
                d.Z -= 1;
            }

            if (_keys.Contains(Key.S) || _keys.Contains(Key.Down))
            {
                d.Z += 1;
            }

            if (_keys.Contains(Key.A) || _keys.Contains(Key.Left))
            {
                d.X -= 1;
            }

            if (_keys.Contains(Key.D) || _keys.Contains(Key.Right))
            {
                d.X += 1;
            }

            if (_keys.Contains(Key.Space))
            {
                up += 1;
            }

            if (_keys.Contains(Key.C))
            {
                up -= 1;
            }

            fast = _keys.Contains(Key.LeftShift) || _keys.Contains(Key.RightShift);
        }
        lock (_camLock)
        {
            if (_eye == EyeMode.Fly && up != 0)
            {
                d.Y = up;
            }
            if (d == Vector3.Zero || dt <= 0)
            {
                return false;
            }
            dt = Math.Min(dt, 0.1f);
            float c = MathF.Cos(_yaw), sn = MathF.Sin(_yaw);
            var flat = new Vector3(d.X * c + d.Z * sn, 0, -d.X * sn + d.Z * c);
            if (_eye == EyeMode.Orbit)
            {
                _target += flat * Math.Max(10, _distance) * dt;
                FollowGround();
            }
            else
            {
                // Walking 6 m/s (Shift: running 12); flying 15 (Shift: 45).
                float speed = _eye == EyeMode.Walk ? (fast ? 12 : 6) : (fast ? 45 : 15);
                _eyePos += (flat + new Vector3(0, d.Y, 0)) * speed * dt;
                KeepOverGround(walk: _eye == EyeMode.Walk);
            }
        }
        return true;
    }

    /// <summary>The target point stays on the ground (or the water) under it; in a dungeon, at its height.</summary>
    private void FollowGround()
    {
        var s = Scene;
        if (s == null || _target.Y > InteriorHeight)
        {
            return;
        }
        _target.Y = GroundOrWater(s, _target);
    }

    /// <summary>The ground's height at the grid point nearest a view-space point (held to the area), or the sea's when higher.</summary>
    /// <param name="s">The area.</param>
    /// <param name="v">The point, in view space.</param>
    private static float GroundOrWater(WorldScene s, Vector3 v)
    {
        int gx = Math.Clamp((int)MathF.Round(v.X + (s.W - 1) / 2f), 0, s.W - 1), gz = Math.Clamp((int)MathF.Round(-v.Z + (s.H - 1) / 2f), 0, s.H - 1);
        return Math.Max(s.Heights[gz * s.W + gx], s.Water);
    }

    /// <summary>Where the camera was at the last frame, in view space.</summary>
    private Vector3 _lastEye;

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
