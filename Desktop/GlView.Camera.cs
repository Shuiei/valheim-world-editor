using System.Numerics;
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
    /// <summary>The orbit camera's distance (metres), turn and tilt (radians) when the editor starts.</summary>
    private const float StartDistance = 140, StartYaw = 0.8f, StartPitch = 0.9f;
    /// <summary>The orbit camera's distance (metres), turn and tilt (radians).</summary>
    private float _distance = StartDistance, _yaw = StartYaw, _pitch = StartPitch;
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

}
