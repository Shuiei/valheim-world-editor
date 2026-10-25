using System.Security.Cryptography;
using TerrainEditor.Editing;
using TerrainEditor.Save;

namespace TerrainEditor.Desktop;

public static partial class HistoryFile
{
    /// <summary>
    /// A zone the world's files hold as ground discs, kept with the history (version 3): its No limit ground as the
    /// session has it, the generated ground that is on, and what the files hold there. Opened again, the world has
    /// the discs; while the files still hold what was written there, the zone's ground is taken back as it was, so an
    /// undo puts back all the ground around the discs too, not only the steps' points.
    /// </summary>
    /// <param name="Edit">The zone's edits, with their lift.</param>
    /// <param name="Bases">The generated ground at each of its points then.</param>
    /// <param name="Written">What the files hold there (<see cref="Written"/>).</param>
    private sealed record KeptLift(ZoneEdit Edit, float[] Bases, long Written);

    /// <summary>
    /// What a zone's edits make of the ground, in short: the height change and the paint at each point (to the
    /// millimetre), as a number to tell whether the files still hold what the editor wrote there.
    /// </summary>
    internal static long Written(ZoneEdit e)
    {
        using var o = new MemoryStream();
        using (var w = new BinaryWriter(o, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            for (int i = 0; i < EditStore.Cells; i++)
            {
                w.Write(e.Modified[i] ? (int)MathF.Round(Math.Clamp(e.Level[i] + e.Smooth[i], -EditStore.MaxLevel, EditStore.MaxLevel) * 1000) : int.MinValue);
                w.Write(e.PaintModified[i]);
                for (int c = 0; e.PaintModified[i] && c < 4; c++)
                {
                    w.Write((int)MathF.Round(e.Paint[i * 4 + c] * 1000));
                }
            }
        }
        return BitConverter.ToInt64(SHA256.HashData(o.ToArray()), 0);
    }

    /// <summary>The No limit ground of the zones the world holds as ground discs, taken with the steps.</summary>
    private static List<KeptLift> TakeLifted(WorldSession w) =>
        [.. w.DiscZones.Select(z => new KeptLift(w.Edits.Get(z.Key.X, z.Key.Z) ?? new ZoneEdit(z.Key.X, z.Key.Z), [.. w.Terrain.BaseZone(z.Key.X, z.Key.Z)], z.Value))];

    /// <summary>Writes the kept zones after the steps.</summary>
    private static void WriteLifted(BinaryWriter o, List<KeptLift> lifted)
    {
        o.Write(lifted.Count);
        foreach (var l in lifted)
        {
            var e = l.Edit;
            o.Write(e.ZoneX);
            o.Write(e.ZoneZ);
            o.Write(l.Written);
            o.Write([.. e.Modified.Select(m => (byte)(m ? 1 : 0))]);
            WriteFloats(o, e.Level);
            WriteFloats(o, e.Smooth);
            o.Write([.. e.PaintModified.Select(m => (byte)(m ? 1 : 0))]);
            WriteFloats(o, e.Paint);
            WriteFloats(o, e.Lift);
            WriteFloats(o, l.Bases);
        }
    }

    /// <summary>Writes floats, one after the other.</summary>
    private static void WriteFloats(BinaryWriter o, float[] values)
    {
        foreach (float f in values)
        {
            o.Write(f);
        }
    }

    /// <summary>Reads the kept zones written by <see cref="WriteLifted"/>.</summary>
    private static List<KeptLift> ReadLifted(BinaryReader i)
    {
        int count = ReadCount(i);
        var lifted = new List<KeptLift>(Capacity(count));
        for (int n = 0; n < count; n++)
        {
            int zx = i.ReadInt32(), zz = i.ReadInt32();
            long written = i.ReadInt64();
            var e = new ZoneEdit(zx, zz)
            {
                Modified = [.. i.ReadExactBytes(EditStore.Cells).Select(b => b != 0)],
                Level = ReadFloats(i, EditStore.Cells),
                Smooth = ReadFloats(i, EditStore.Cells),
                PaintModified = [.. i.ReadExactBytes(EditStore.Cells).Select(b => b != 0)],
                Paint = ReadFloats(i, EditStore.Cells * 4),
                Lift = ReadFloats(i, EditStore.Cells),
            };
            lifted.Add(new KeptLift(e, ReadFloats(i, EditStore.Cells), written));
        }
        return lifted;
    }

    /// <summary>
    /// Takes back the No limit ground of the kept zones whose world still holds what was written there: onto the
    /// generated ground as it is now (the discs in it), so it shows as it did, as what the world holds (nothing to save
    /// or apply, EditStore.PutPlanned). A zone changed since (players dug there) keeps what the world holds.
    /// </summary>
    private static void TakeLiftedBack(WorldSession w, List<KeptLift> lifted)
    {
        foreach (var l in lifted)
        {
            var e = l.Edit;
            if (Written(w.Edits.Get(e.ZoneX, e.ZoneZ) ?? new ZoneEdit(e.ZoneX, e.ZoneZ)) != l.Written)
            {
                continue;
            }
            float[] b = w.Terrain.BaseZone(e.ZoneX, e.ZoneZ);
            for (int k = 0; k < EditStore.Cells; k++)
            {
                e.Lift[k] = EditStore.KeepLift(e.Lift[k] + l.Bases[k] - b[k]);
            }
            w.Edits.PutPlanned(e);
            w.KeepDiscZone((e.ZoneX, e.ZoneZ), l.Written);
        }
    }
}
