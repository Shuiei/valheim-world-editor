using System.Text.Json;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>--driver's docs command: the Help window opened at a place, searched, pictured and closed.</summary>
public class DriverDocsTests
{
    [AvaloniaFact]
    public async Task TheHelpWindowIsOpenedSearchedPicturedAndClosed()
    {
        var w = new MainWindow(load: false);
        string picture = Path.Combine(Path.GetTempPath(), $"vwe-docs-{Guid.NewGuid():N}.png");
        try
        {
            async Task<JsonElement> Send(string cmd)
            {
                string answer = await Driver.Run(w, cmd);
                Dispatcher.UIThread.RunJobs();
                return JsonDocument.Parse(answer).RootElement.Clone();
            }
            var opened = await Send("docs map.md#controls");
            Assert.Equal("map.md", opened.GetProperty("page").GetString());
            Assert.Equal("controls", opened.GetProperty("anchor").GetString());
            Assert.Equal("select.md", (await Send("docs select.md")).GetProperty("page").GetString());
            Assert.True((await Send("docs search select box")).GetProperty("hits").GetInt32() > 0);
            Assert.Equal("select.md", (await Send("docs shot " + picture)).GetProperty("page").GetString());
            Assert.True(new FileInfo(picture).Length > 0);
            Assert.Equal("{}", (await Send("docs close")).GetRawText());
            Assert.Null(HelpWindow.Current);
            await Assert.ThrowsAsync<InvalidOperationException>(() => Driver.Run(w, "docs shot " + picture));
            await Assert.ThrowsAsync<ArgumentException>(() => Driver.Run(w, "docs"));
        }
        finally
        {
            HelpWindow.Current?.Close();
            w.Close();
            File.Delete(picture);
        }
    }
}
