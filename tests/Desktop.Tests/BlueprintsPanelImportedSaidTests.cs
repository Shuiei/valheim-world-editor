using TerrainEditor.App;
using Avalonia.Headless.XUnit;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// What an import says: its name and count, and each thing left out (terrain, kinds this world cannot make, lines that could
/// not be read) changes the message, and the reminder to install Homestead is added while it is not installed.
/// </summary>
public class BlueprintsPanelImportedSaidTests
{
    /// <summary>
    /// A panel whose Homestead is installed or not.
    /// </summary>
    private static BlueprintsPanel Panel(bool installed) => new(new PasteTool(), () => null)
    {
        FindHomestead = () => new Homestead.Status(installed, installed ? "1.3.2" : null, installed ? ["test"] : [], Path.GetTempPath()),
    };

    [AvaloniaFact]
    public void EachThingLeftOutChangesTheMessage()
    {
        var b = Panel(installed: true);
        string plain = b.ImportedSaid("Pen", 3, false, [], 0).English;

        Assert.NotEqual(plain, b.ImportedSaid("Pen", 3, true, [], 0).English);
        Assert.NotEqual(plain, b.ImportedSaid("Pen", 3, false, ["Unknown"], 0).English);
        Assert.NotEqual(plain, b.ImportedSaid("Pen", 3, false, [], 2).English);
    }

    [AvaloniaFact]
    public void TheReminderIsAddedOnlyWhileHomesteadIsNotInstalled()
    {
        string installed = Panel(installed: true).ImportedSaid("Pen", 3, false, [], 0).English;
        string missing = Panel(installed: false).ImportedSaid("Pen", 3, false, [], 0).English;

        Assert.NotEqual(installed, missing);
    }
}
