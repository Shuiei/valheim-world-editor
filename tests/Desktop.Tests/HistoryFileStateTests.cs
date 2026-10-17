using System.Numerics;
using System.Text;
using WorldEditor.Tests;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// What a history file keeps of a vector and of a step's ground values, written and read back as they were.
/// </summary>
public class HistoryFileStateTests
{
    /// <summary>The bytes written by <paramref name="write"/>, rewound for reading.</summary>
    private static MemoryStream Written(Action<BinaryWriter> write)
    {
        var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            write(writer);
        }
        stream.Position = 0;
        return stream;
    }

    [Fact]
    public void AVectorIsWrittenAsThreeFloatsOfTwelveBytes()
    {
        using var stream = Written(w => PrivateMember.Call(typeof(HistoryFile), "WriteVector", null, w, new Vector3(1f, 2f, 3f)));

        Assert.Equal(12, stream.Length);
    }

    [Fact]
    public void AVectorReadsBackAsItWasWritten()
    {
        using var stream = Written(w => PrivateMember.Call(typeof(HistoryFile), "WriteVector", null, w, new Vector3(1f, -2f, 3.5f)));
        using var reader = new BinaryReader(stream);

        Assert.Equal(new Vector3(1f, -2f, 3.5f), PrivateMember.Call<Vector3>(typeof(HistoryFile), "ReadVector", null, reader));
    }

    [Fact]
    public void AStepsGroundValuesReadBackAsTheyWereWritten()
    {
        var state = new Ground.State(
            [1f, 2f],
            [3f, 4f],
            [5, 6],
            [1f, 2f, 3f, 4f, 5f, 6f, 7f, 8f],
            [7, 8],
            [9f, 10f]);
        using var stream = Written(w => PrivateMember.Call(typeof(HistoryFile), "WriteState", null, w, state));
        using var reader = new BinaryReader(stream);

        var back = PrivateMember.Call<Ground.State>(typeof(HistoryFile), "ReadState", null, reader, 2);

        Assert.Equal(state.Level, back.Level);
        Assert.Equal(state.Smooth, back.Smooth);
        Assert.Equal(state.Mod, back.Mod);
        Assert.Equal(state.Paint, back.Paint);
        Assert.Equal(state.PMod, back.PMod);
        Assert.Equal(state.Lift, back.Lift);
    }
}
