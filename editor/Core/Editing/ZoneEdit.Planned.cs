using System.Security.Cryptography;

namespace TerrainEditor.Editing;

public sealed partial class ZoneEdit
{
    /// <summary>The bytes <see cref="Fingerprint"/> takes per grid point: the paint flag, the height change, the lift and the paint.</summary>
    private const int FingerprintBytes = 1 + 2 * 4 + 4 * 4;

    /// <summary>
    /// The zone's ground with its lift, as one number: whether its No limit ground is still the one its ground discs
    /// were worked out from (EditStore.PutPlanned). It takes what <see cref="SameGround"/> compares, exactly: the
    /// height change where the point is changed, the lift, and the paint where it is painted.
    /// </summary>
    public long Fingerprint()
    {
        byte[] raw = new byte[EditStore.Cells * FingerprintBytes];
        for (int i = 0; i < EditStore.Cells; i++)
        {
            var at = raw.AsSpan(i * FingerprintBytes);
            at[0] = (byte)(PaintModified[i] ? 1 : 0);
            BitConverter.TryWriteBytes(at[1..], Modified[i] ? Level[i] + Smooth[i] : 0f);
            BitConverter.TryWriteBytes(at[5..], Lift[i]);
            for (int c = 0; PaintModified[i] && c < 4; c++)
            {
                BitConverter.TryWriteBytes(at[(9 + c * 4)..], Paint[i * 4 + c]);
            }
        }
        return BitConverter.ToInt64(SHA256.HashData(raw), 0);
    }

    /// <summary>
    /// Puts the zone's ground values from one generated ground onto another (ground discs made or taken away under it):
    /// the lift takes the difference, so the zone gives the same ground.
    /// </summary>
    /// <param name="from">The generated ground the values were on, per grid point.</param>
    /// <param name="to">The generated ground they go onto.</param>
    public void Rebase(float[] from, float[] to)
    {
        for (int i = 0; i < EditStore.Cells; i++)
        {
            Lift[i] = EditStore.KeepLift(Lift[i] + from[i] - to[i]);
        }
    }
}
