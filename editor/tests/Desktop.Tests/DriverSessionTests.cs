using Avalonia.Headless.XUnit;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>--driver's note command: the locations note closed as its × closes it, anything else refused.</summary>
public class DriverSessionTests
{
    [AvaloniaFact]
    public async Task NoteCloseHidesTheLocationsNote()
    {
        var w = new MainWindow(load: false);
        try
        {
            w.LocationNote.IsVisible = true;
            await Driver.Run(w, "note close");
            Assert.False(w.LocationNote.IsVisible);
            await Assert.ThrowsAsync<InvalidOperationException>(() => Driver.Run(w, "note open"));
        }
        finally
        {
            w.Close();
        }
    }
}
