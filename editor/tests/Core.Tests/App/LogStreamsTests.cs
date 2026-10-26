using System.Text;
using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The log's streams when a writer fails (Log.Sink and Log.Tee): a log file that cannot be written is said once on the
/// error output and every text is still tried, an error output that fails too is let go of, and a console stream has
/// its console's encoding.
/// </summary>
public class LogStreamsTests
{
    private static readonly Type SinkType = PrivateMember.Nested(typeof(Log), "Sink");
    private static readonly Type TeeType = PrivateMember.Nested(typeof(Log), "Tee");

    /// <summary>A writer that refuses every text (a full disk), counting the tries.</summary>
    private sealed class Refusing : TextWriter
    {
        public int Tries { get; private set; }

        public override Encoding Encoding => Encoding.UTF8;

        public override void Write(char value) => Refuse();

        public override void Write(string? value) => Refuse();

        private void Refuse()
        {
            Tries++;
            throw new IOException("disk full");
        }
    }

    [Fact]
    public void ALogFileThatCannotBeWrittenIsSaidOnceOnTheErrorOutput()
    {
        var file = new Refusing();
        var error = new StringWriter();
        object sink = PrivateMember.New(SinkType, file, error);

        PrivateMember.Call(SinkType, "Write", sink, "first line\n");
        PrivateMember.Call(SinkType, "Write", sink, "second line\n");

        Assert.Equal("The log file could not be written: System.IO.IOException: disk full" + Environment.NewLine, error.ToString());
        // The file is tried with every text, in case it comes back.
        Assert.Equal(2, file.Tries);
    }

    [Fact]
    public void AnErrorOutputThatCannotBeWrittenEitherIsLetGoOf()
    {
        var error = new Refusing();
        object sink = PrivateMember.New(SinkType, new Refusing(), error);

        PrivateMember.Call(SinkType, "Write", sink, "line");
        PrivateMember.Call(SinkType, "Write", sink, "another");

        // Nothing thrown, and the failure was said once only.
        Assert.Equal(1, error.Tries);
    }

    [Fact]
    public void AConsoleStreamHasItsConsolesEncodingAndWritesToBoth()
    {
        var console = new StringWriter();
        var file = new StringWriter();
        object sink = PrivateMember.New(SinkType, file, new StringWriter());
        var tee = (TextWriter)PrivateMember.New(TeeType, console, sink);

        tee.Write("hello");

        Assert.Equal(console.Encoding, tee.Encoding);
        Assert.Equal("hello", console.ToString());
        Assert.EndsWith(" hello", file.ToString());
    }
}
