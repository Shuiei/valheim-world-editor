
namespace TerrainEditor.Desktop;

public sealed partial class SelectTool
{
    /// <summary>
    /// Moves the selection there (by: by these amounts), as one undo step; typed values are not snapped. An axis given
    /// no value (null) is left as it is, and with none given there is no move.
    /// </summary>
    /// <param name="x">World x of the middle, in metres (or how far to move); null leaves it.</param>
    /// <param name="y">Height of the lowest object, in metres (or how far to lift); null leaves it.</param>
    /// <param name="z">World z of the middle, in metres (or how far to move); null leaves it.</param>
    /// <param name="turn">The first object's turn, in degrees (or how far to turn); null leaves it.</param>
    /// <param name="by">The values are amounts to move by, not a place.</param>
    public void PlaceAt(float? x, float? y, float? z, float? turn, bool by)
    {
        Commit();
        // Checked on what was asked, not the offsets worked out below: keeping the height adds a lift that is not zero.
        if (x is null && y is null && z is null && turn is null || Where() is not { } w || Begin() is not { } m)
        {
            return;
        }
        m.Dx += Amount(x, w.X, by);
        m.Dz += Amount(z, w.Z, by);
        m.Turn += Amount(turn, w.Turn, by);
        m.NoSnap = true;
        GroundBlock(m);
        var low = m.Items.MinBy(it => it.Thing.Position.Y)!;
        float now = Target(m, low).Y;
        m.Dy += low.Thing.Position.Y + Amount(y, w.Y, by) - now;
        Commit();
    }

    /// <summary>How far one axis of <see cref="PlaceAt"/> moves: nothing when no value is given.</summary>
    /// <param name="value">The value given, or null.</param>
    /// <param name="from">Where that axis is now.</param>
    /// <param name="by">The value is an amount, not a place.</param>
    private static float Amount(float? value, float from, bool by)
    {
        if (value is not { } v)
        {
            return 0;
        }
        return by ? v : v - from;
    }
}
