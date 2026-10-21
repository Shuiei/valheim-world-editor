using System.Numerics;

namespace TerrainEditor.Desktop;

// The keys that move the camera: which are held, and the move they make.
public sealed partial class GlView
{
    /// <summary>
    /// Moves the camera by the keys held (WASD or arrows; Space and C up and down when flying; Shift faster when
    /// walking or flying) for <paramref name="dt"/> seconds (at most 0.1). True when it moved.
    /// </summary>
    internal bool MoveWithKeys(float dt)
    {
        var (d, up, fast) = HeldKeys();
        lock (_camLock)
        {
#pragma warning disable S1244 // up is a sum of whole steps (+1 for Space, -1 for C): exactly 0 when neither or both are held.
            if (_eye == EyeMode.Fly && up != 0)
#pragma warning restore S1244
            {
                d.Y = up;
            }
            if (d == Vector3.Zero || dt <= 0)
            {
                return false;
            }
            dt = Math.Min(dt, 0.1f);
            MoveBy(d, dt, fast);
        }
        return true;
    }

    /// <summary>
    /// Moves the orbit's target or the walking or flying eye along the keys' direction, turned with the view, for dt
    /// seconds. Under the camera lock.
    /// </summary>
    /// <param name="d">The keys' direction: X right, Z back, Y up (flying only).</param>
    /// <param name="dt">The seconds moved for (at most 0.1).</param>
    /// <param name="fast">Shift held: running, or flying faster.</param>
    internal void MoveBy(Vector3 d, float dt, bool fast)
    {
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
}
