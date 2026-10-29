using ValheimWorldEditor.Sdk;

namespace Riverheim.WorldEditor.Tests;

/// <summary>A world file's bytes as a generator reads and writes them (the game's ZPackage forms, over BinaryReader and BinaryWriter).</summary>
internal static class WorldFile
{
    /// <summary>The bytes a write makes.</summary>
    public static byte[] Written(Action<IWorldFileWriter> write)
    {
        using var bytes = new MemoryStream();
        using (var w = new BinaryWriter(bytes))
        {
            write(new Writer(w));
        }
        return bytes.ToArray();
    }

    /// <summary>The bytes a BinaryWriter makes, as Riverheim's game side writes them.</summary>
    public static byte[] AsTheGameWrites(Action<BinaryWriter> write)
    {
        using var bytes = new MemoryStream();
        using (var w = new BinaryWriter(bytes))
        {
            write(w);
        }
        return bytes.ToArray();
    }

    /// <summary>A reader of these bytes.</summary>
    public static IWorldFileReader Reader(byte[] bytes) => new Read(new BinaryReader(new MemoryStream(bytes)));

    private sealed class Read(BinaryReader r) : IWorldFileReader
    {
        public int ReadInt() => r.ReadInt32();

        public string ReadString() => r.ReadString();

        public byte ReadByte() => r.ReadByte();

        public int ReadCount()
        {
            int n = r.ReadByte();
            return (n & 0x80) != 0 ? ((n & 0x7F) << 8) | r.ReadByte() : n;
        }

        public long ReadLong() => r.ReadInt64();

        public float ReadSingle() => r.ReadSingle();

        public bool ReadBool() => r.ReadBoolean();
    }

    private sealed class Writer(BinaryWriter w) : IWorldFileWriter
    {
        public void WriteInt(int value) => w.Write(value);

        public void WriteString(string value) => w.Write(value);

        public void WriteByte(byte value) => w.Write(value);

        public void WriteLong(long value) => w.Write(value);

        public void WriteSingle(float value) => w.Write(value);

        public void WriteBool(bool value) => w.Write(value);
    }
}
