using System.Buffers.Binary;
using System.Text;

namespace WorldEditor.Tests.Unity;

/// <summary>
/// Writes a Unity asset bundle (UnityFS, format 8): a header, the blocks-and-directory info (the blocks' sizes and the
/// files the bundle holds), then its files one after the other, cut into blocks stored as they are or LZ4-compressed.
/// The LZ4 written is the simplest valid kind (each block one run of literals): what is read is LZ4 all the same.
/// </summary>
public sealed class UnityFsWriter
{
    /// <summary>
    /// The files of the bundle: name, bytes, and whether it is a serialized file (else a resource, as .resS).
    /// </summary>
    private readonly List<(string Name, byte[] Data, bool Serialized)> _files = [];

    /// <summary>
    /// Compress the blocks (and the blocks info) with LZ4, as the game's bundles are.
    /// </summary>
    public bool Lz4 { get; init; } = true;

    /// <summary>
    /// The largest block, in bytes (Unity's 128 KiB; smaller to have reads cross blocks).
    /// </summary>
    public int BlockSize { get; init; } = 1 << 17;

    /// <summary>
    /// Adds a serialized file (an assets file, "CAB-..." by Unity's naming).
    /// </summary>
    public UnityFsWriter File(string name, byte[] data)
    {
        _files.Add((name, data, true));
        return this;
    }

    /// <summary>
    /// Adds a resource file (data kept out of objects: "CAB-....resS").
    /// </summary>
    public UnityFsWriter Resource(string name, byte[] data)
    {
        _files.Add((name, data, false));
        return this;
    }

    /// <summary>
    /// Writes the bundle to a file.
    /// </summary>
    public void Save(string path) => System.IO.File.WriteAllBytes(path, ToBytes());

    /// <summary>
    /// The whole bundle.
    /// </summary>
    public byte[] ToBytes()
    {
        byte[] data = [.. _files.SelectMany(f => f.Data)];
        // The blocks, each stored or compressed.
        var blocks = new List<(int Size, byte[] Stored)>();
        for (int at = 0; at < data.Length || blocks.Count == 0; at += BlockSize)
        {
            var part = data.AsSpan(at, Math.Min(BlockSize, data.Length - at));
            blocks.Add((part.Length, Lz4 ? Literals(part) : part.ToArray()));
        }
        // The blocks-and-directory info, big-endian.
        using var info = new MemoryStream();
        using (var w = new BinaryWriter(info, Encoding.UTF8, leaveOpen: true))
        {
            w.Write(new byte[16]); // hash of the data
            w.Write(Be(blocks.Count));
            foreach (var (size, stored) in blocks)
            {
                w.Write(Be(size));
                w.Write(Be(stored.Length));
                w.Write(Be16(Lz4 ? (short)2 : (short)0));
            }
            w.Write(Be(_files.Count));
            long offset = 0;
            foreach (var (name, bytes, serialized) in _files)
            {
                w.Write(Be(offset));
                w.Write(Be((long)bytes.Length));
                w.Write(Be(serialized ? 4 : 0));
                w.Write(Encoding.UTF8.GetBytes(name));
                w.Write((byte)0);
                offset += bytes.Length;
            }
        }
        byte[] rawInfo = info.ToArray();
        byte[] storedInfo = Lz4 ? Literals(rawInfo) : rawInfo;
        using var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true))
        {
            w.Write("UnityFS\0"u8);
            w.Write(Be(8));
            w.Write("5.x.x\0"u8);
            w.Write("2022.3.62f1\0"u8);
            long sizeAt = ms.Position;
            w.Write(0L); // the bundle's size, below
            w.Write(Be(storedInfo.Length));
            w.Write(Be(rawInfo.Length));
            // The info's compression (2: LZ4) and 0x40: the blocks and directory in one info, at the start.
            w.Write(Be((Lz4 ? 2 : 0) | 0x40));
            // From format 7 the info starts 16-byte aligned.
            while (ms.Position % 16 != 0)
            {
                w.Write((byte)0);
            }
            w.Write(storedInfo);
            foreach (var (_, stored) in blocks)
            {
                w.Write(stored);
            }
            long total = ms.Position;
            ms.Position = sizeAt;
            w.Write(Be(total));
        }
        return ms.ToArray();
    }

    /// <summary>
    /// Bytes as one LZ4 block of literals only: a token with the length (15 and more as extra bytes), then the bytes.
    /// </summary>
    public static byte[] Literals(ReadOnlySpan<byte> data)
    {
        var res = new List<byte>(data.Length + data.Length / 255 + 16);
        int n = data.Length;
        res.Add((byte)(Math.Min(n, 15) << 4));
        if (n >= 15)
        {
            int rest = n - 15;
            while (rest >= 255)
            {
                res.Add(255);
                rest -= 255;
            }
            res.Add((byte)rest);
        }
        res.AddRange(data);
        return [.. res];
    }

    /// <summary>
    /// A 32-bit number's bytes, big-endian.
    /// </summary>
    private static byte[] Be(int v)
    {
        byte[] b = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(b, v);
        return b;
    }

    /// <summary>
    /// A 64-bit number's bytes, big-endian.
    /// </summary>
    private static byte[] Be(long v)
    {
        byte[] b = new byte[8];
        BinaryPrimitives.WriteInt64BigEndian(b, v);
        return b;
    }

    /// <summary>
    /// A 16-bit number's bytes, big-endian.
    /// </summary>
    private static byte[] Be16(short v)
    {
        byte[] b = new byte[2];
        BinaryPrimitives.WriteInt16BigEndian(b, v);
        return b;
    }
}
