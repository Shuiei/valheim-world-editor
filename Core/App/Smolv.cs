namespace TerrainEditor.App;

/// <summary>
/// SMOL-V to SPIR-V, for Unity's Vulkan shaders (Unity keeps each Vulkan program SMOL-V compressed): a
/// port of the decoder in SMOL-V by Aras Pranckevicius (https://github.com/aras-p/smol-v, MIT license,
/// Copyright (c) 2016-2024 Aras Pranckevicius), encoding versions 0 and 1.
/// </summary>
public static class Smolv
{
    /// <summary>The first word of a SPIR-V module.</summary>
    private const uint SpirvMagic = 0x07230203;
    /// <summary>The first four bytes of a SMOL-V program.</summary>
    private static readonly byte[] Magic = "LOMS"u8.ToArray();

    /// <summary>
    /// The SPIR-V size in bytes of the SMOL-V program at data[at..], or 0 when it is not one (no magic, or a size
    /// past int's range).
    /// </summary>
    public static int DecodedSize(byte[] data, int at = 0) =>
        at + 24 <= data.Length && data.AsSpan(at, 4).SequenceEqual(Magic) && BitConverter.ToUInt32(data, at + 20) is var size && size <= int.MaxValue
            ? (int)size
            : 0;

    /// <summary>
    /// Whether the data after a program's header can hold its decoded size: each byte decodes to four words at most
    /// (a shuffle's swizzle), the header to five.
    /// </summary>
    private static bool CanHold(byte[] data, int at, int size) => size <= 20 + 16L * (data.Length - at - 24);

    /// <summary>The SPIR-V words of the SMOL-V program starting at data[at..] (data may hold more after it).</summary>
    /// <exception cref="InvalidDataException">Not SMOL-V, an unknown encoding version, or broken data.</exception>
    public static uint[] Decode(byte[] data, int at = 0) => Decode(data, at, out _);

    /// <summary>
    /// The SPIR-V words of the SMOL-V program starting at data[at..], and where in data it ends (data may hold more
    /// after it).
    /// </summary>
    /// <exception cref="InvalidDataException">Not SMOL-V, an unknown encoding version, or broken data.</exception>
    private static uint[] Decode(byte[] data, int at, out int end)
    {
        int size = DecodedSize(data, at);
        if (size == 0)
        {
            throw new InvalidDataException("not SMOL-V data");
        }
        if (!CanHold(data, at, size))
        {
            throw new InvalidDataException("SMOL-V data ends early");
        }
        uint version = BitConverter.ToUInt32(data, at + 4), generator = BitConverter.ToUInt32(data, at + 8), bound = BitConverter.ToUInt32(data, at + 12), schema = BitConverter.ToUInt32(data, at + 16);
        int smolVersion = (int)(version >> 24);
        if (!SmolvOpcodes.Known.TryGetValue(smolVersion, out int known))
        {
            throw new InvalidDataException($"SMOL-V encoding version {smolVersion} is not known");
        }
        var output = new List<uint>(size / 4) { SpirvMagic, version & 0xFFFFFF, generator, bound, schema };
        var decoder = new SmolvDecoder(data, at + 24, output, size / 4, known);
        decoder.Run();
        if (output.Count != size / 4)
        {
            throw new InvalidDataException("SMOL-V data decodes to the wrong size");
        }
        end = decoder.End;
        return [.. output];
    }

    /// <summary>
    /// (offset, SPIR-V words) of every SMOL-V program in a blob; the next one is looked for after the end of the one
    /// decoded, never inside its data.
    /// </summary>
    public static IEnumerable<(int At, uint[] Words)> Programs(byte[] data)
    {
        int at = data.AsSpan().IndexOf(Magic);
        while (at >= 0)
        {
            int from = at + 4;
            // A chance match of the magic (a size the data cannot hold) is passed over.
            if (DecodedSize(data, at) is > 0 and var size && CanHold(data, at, size))
            {
                yield return (at, Decode(data, at, out from));
            }
            int next = data.AsSpan(from).IndexOf(Magic);
            at = next < 0 ? -1 : from + next;
        }
    }
}
