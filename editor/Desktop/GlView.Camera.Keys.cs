using System.Numerics;
using Avalonia.Input;

namespace TerrainEditor.Desktop;

// The movement keys held now (WASD, arrows, Space, C and Shift), read as the camera moves.
public sealed partial class GlView
{
    /// <summary>
    /// The movement keys held now: the flat direction (X: right, Z: back), the up (Space) or down (C) amount, and
    /// whether Shift is held.
    /// </summary>
    internal (Vector3 Direction, float Up, bool Fast) HeldKeys()
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
        return (d, up, fast);
    }
}
