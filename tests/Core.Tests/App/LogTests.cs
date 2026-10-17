using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The log file the console is copied into: each line with its time, whatever way the text was written. It takes the
/// console and the data folder, so it runs alone.
/// </summary>
[Collection("DataDir")]
public sealed class LogTests
{
    [Fact]
    public void TextWrittenAnyWayMakesWholeTimedLines()
    {
        var (output, error, data) = (Console.Out, Console.Error, AppSettings.DataDirOverride);
        string dir = Path.Combine(Path.GetTempPath(), "vwe-log-" + Guid.NewGuid().ToString("N")[..8]);
        string text;
        try
        {
            AppSettings.DataDirOverride = dir;
            Log.Start("test");
            // Windows' line end, by WriteLine(); characters from an array; a span.
            Console.Out.NewLine = "\r\n";
            Console.Out.Write("abc".ToCharArray(), 0, 3);
            Console.Out.WriteLine();
            Console.Out.Write("def".AsSpan());
            Console.Out.WriteLine();
            text = Log.Read();
        }
        finally
        {
            Console.SetOut(output);
            Console.SetError(error);
            AppSettings.DataDirOverride = data;
        }
        try
        {
            // (The header's lines are written as the system ends lines; the console's end with \n alone.)
            string[] lines = text.Split('\n');
            Assert.Matches(@"^\d\d:\d\d:\d\d abc$", lines[^3]);
            Assert.Matches(@"^\d\d:\d\d:\d\d def$", lines[^2]);
            Assert.Equal("", lines[^1]);
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }
}
