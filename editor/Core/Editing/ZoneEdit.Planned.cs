using System.Security.Cryptography;

namespace TerrainEditor.Editing;

public sealed partial class ZoneEdit
{
    /// <summary>The bytes <see cref="Fingerprint"/> takes per grid point: the flags, level, smoothing, lift and paint.</summary>
    private const int FingerprintBytes = 1 + 3 * 4 + 4 * 4;

    /// <summary>
    /// The zone's ground with its lift, as one number: whether its No limit ground is still the one its ground discs
    /// were worked out from (EditStore.PutPlanned).
    /// </summary>
    public long Fingerprint()
    {
        byte[] raw = new byte[EditStore.Cells * FingerprintBytes];
        for (int i = 0; i < EditStore.Cells; i++)
        {
            var at = raw.AsSpan(i * FingerprintBytes);
            at[0] = (byte)((Modified[i] ? 1 : 0) | (PaintModified[i] ? 2 : 0));
            BitConverter.TryWriteBytes(at[1..], Level[i]);
            BitConverter.TryWriteBytes(at[5..], Smooth[i]);
            BitConverter.TryWriteBytes(at[9..], Lift[i]);
            for (int c = 0; c < 4; c++)
            {
                BitConverter.TryWriteBytes(at[(13 + c * 4)..], Paint[i * 4 + c]);
            }
        }
        return BitConverter.ToInt64(SHA256.HashData(raw), 0);
    }
}
