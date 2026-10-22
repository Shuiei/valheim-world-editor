using TerrainEditor.App;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Settings window's world folders hint: the usual folders found, or a whole sentence when there are none.
/// </summary>
public class SettingsDialogFormLayoutTests
{
    [Fact]
    public void TheWorldsHintListsTheUsualFoldersFound()
    {
        Assert.Equal(Lang.T("settings.worldsHint", "~/a, ~/b"), SettingsDialog.Form.WorldsHint(["~/a", "~/b"]));
    }

    [Fact]
    public void TheWorldsHintSaysSoWhenNoUsualFolderIsThere()
    {
        string hint = SettingsDialog.Form.WorldsHint([]);
        Assert.Equal(Lang.T("settings.worldsHintNone"), hint);
    }
}
