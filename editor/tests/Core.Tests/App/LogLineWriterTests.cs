using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The log's line writer: each line it starts gets the time, carriage returns are dropped, and text written in parts
/// continues the line it was in. It writes to the log file and the error output, and takes nothing from the console.
/// </summary>
public sealed class LogLineWriterTests
{
    /// <summary>A line written in two parts is one line with one time.</summary>
    [Fact]
    public void TextWrittenInPartsContinuesTheSameLine()
    {
        using var file = new StringWriter();
        object sink = NewSink(file);

        Write(sink, "ab");
        Write(sink, "cd\n");

        string[] lines = file.ToString().Split('\n');
        Assert.Matches(@"^\d\d:\d\d:\d\d abcd$", lines[0]);
        Assert.Equal("", lines[1]);
    }

    /// <summary>A Windows line end is written as one line end, with the time before each line.</summary>
    [Fact]
    public void CarriageReturnsAreDroppedAndEachLineGetsItsTime()
    {
        using var file = new StringWriter();
        object sink = NewSink(file);

        Write(sink, "first\r\nsecond");

        string[] lines = file.ToString().Split('\n');
        Assert.Matches(@"^\d\d:\d\d:\d\d first$", lines[0]);
        Assert.Matches(@"^\d\d:\d\d:\d\d second$", lines[1]);
    }

    /// <summary>Empty text writes nothing at all.</summary>
    [Fact]
    public void EmptyTextWritesNothing()
    {
        using var file = new StringWriter();
        object sink = NewSink(file);

        Write(sink, "");
        Write(sink, null);

        Assert.Equal("", file.ToString());
    }

    /// <summary>Makes a line writer over a log file, with the error output kept aside.</summary>
    /// <param name="file">The log file's text.</param>
    private static object NewSink(TextWriter file) =>
        PrivateMember.New(PrivateMember.Nested(typeof(Log), "Sink"), file, new StringWriter());

    /// <summary>Writes text through a line writer.</summary>
    /// <param name="sink">The line writer.</param>
    /// <param name="text">The text.</param>
    private static void Write(object sink, string? text) =>
        PrivateMember.Call(sink.GetType(), "Write", sink, text);
}
