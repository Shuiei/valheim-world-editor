using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;

namespace TerrainEditor.Desktop;

// The editor side of the answer: reading what the process wrote, and the byte helpers of the wire format.
public static partial class ScriptHost
{
    /// <summary>
    /// Reads the process's answer: the changes; the script's error or a stop thrown on; and no answer, or one cut
    /// short, as a Crash with the process's exit code and what it wrote on its error output.
    /// </summary>
    /// <param name="answer">What the process wrote on its output.</param>
    /// <param name="exitCode">How it ended.</param>
    /// <param name="errors">What it wrote on its error output.</param>
    internal static Changes ReadAnswer(Stream answer, int exitCode, string errors)
    {
        try
        {
            using var r = new BinaryReader(answer, Encoding.UTF8, leaveOpen: true);
            switch ((Answer)r.ReadByte())
            {
                case Answer.Changes:
                {
                    var ch = new Changes();
                    int heights = r.ReadInt32();
                    for (int i = 0; i < heights; i++)
                    {
                        ch.Heights[r.ReadInt32()] = r.ReadSingle();
                    }
                    int paints = r.ReadInt32();
                    for (int i = 0; i < paints; i++)
                    {
                        ch.Paint[r.ReadInt32()] = Floats(r);
                    }
                    int removes = r.ReadInt32();
                    for (int i = 0; i < removes; i++)
                    {
                        ch.Remove.Add(r.ReadInt32());
                    }
                    int adds = r.ReadInt32();
                    for (int i = 0; i < adds; i++)
                    {
                        ch.Add.Add((r.ReadInt32(), r.ReadString(), Vector(r), Vector(r), r.ReadSingle(), r.ReadBoolean()));
                    }
                    int moves = r.ReadInt32();
                    for (int i = 0; i < moves; i++)
                    {
                        ch.Moves[r.ReadInt32()] = (Vector(r), Vector(r), r.ReadSingle());
                    }
                    ch.Output.Append(r.ReadString());
                    ch.NoLimit = r.ReadBoolean();
                    return ch;
                }
                case Answer.Error:
                    throw new ScriptException(r.ReadString(), r.ReadString(), r.ReadString());
                case Answer.Stopped:
                    throw new OperationCanceledException("the script was stopped");
                default:
                    throw new InvalidDataException("the script's process answered something else");
            }
        }
        catch (Exception e) when (e is EndOfStreamException or InvalidDataException)
        {
            string said = errors.Trim();
            throw new ScriptException("", "Crash", $"the script's process ended without an answer (exit code {exitCode}){(said.Length > 0 ? ":\n" + said : "")}", e);
        }
    }

    /// <summary>Reads this many bytes, all of them (a short read is the end of the stream).</summary>
    private static byte[] Exactly(BinaryReader r, int count)
    {
        byte[] bytes = new byte[count];
        r.BaseStream.ReadExactly(bytes);
        return bytes;
    }

    /// <summary>Reads a block of floats (Block of their bytes).</summary>
    private static float[] Floats(BinaryReader r)
    {
        float[] a = new float[r.ReadInt32() / sizeof(float)];
        r.BaseStream.ReadExactly(MemoryMarshal.AsBytes(a.AsSpan()));
        return a;
    }

    /// <summary>Reads a block of ints (Block of their bytes).</summary>
    private static int[] Ints(BinaryReader r)
    {
        int[] a = new int[r.ReadInt32() / sizeof(int)];
        r.BaseStream.ReadExactly(MemoryMarshal.AsBytes(a.AsSpan()));
        return a;
    }

    /// <summary>Writes a vector's three floats.</summary>
    private static void Write(BinaryWriter w, Vector3 v)
    {
        w.Write(v.X);
        w.Write(v.Y);
        w.Write(v.Z);
    }

    /// <summary>Reads a vector's three floats.</summary>
    private static Vector3 Vector(BinaryReader r) => new(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
}
