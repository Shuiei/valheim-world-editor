using Avalonia.Media;
using TerrainEditor.App;
namespace TerrainEditor.Desktop;

public sealed partial class BlueprintsPanel
{
    /// <summary>Shows or hides the panel, and refreshes it.</summary>
    /// <param name="open">True shows, false hides, null switches.</param>
    public void Toggle(bool? open = null)
    {
        Card.IsVisible = open ?? !Card.IsVisible;
        Refresh();
    }

    /// <summary>Looks for Homestead again and, while the panel is shown, lists the blueprints again.</summary>
    public void Refresh()
    {
        LookAgain();
        if (!Card.IsVisible)
        {
            return;
        }
        Library.Render();
        if (Status.Installed)
        {
            Banner.Text = Lang.T("blueprints.installed", Status.Version, string.Join(", ", Status.Where.Distinct()));
            Banner.Foreground = Ui.Muted;
            _bannerBox.Background = Brushes.Transparent;
            GetHomesteadButton.IsVisible = false;
        }
        else
        {
            Banner.Text = Lang.T("blueprints.notInstalled");
            Banner.Foreground = new SolidColorBrush(Color.FromRgb(240, 180, 90));
            _bannerBox.Background = new SolidColorBrush(Color.FromArgb(40, 240, 160, 60));
            GetHomesteadButton.IsVisible = true;
        }
        _folder.Text = Lang.T("blueprints.folder", Ui.Tilde(Status.Folder)) + (Library.Listed.Older.Count > 0 ? " " + Lang.T("blueprints.folderOlder", Ui.Tilde(Store.Directory)) : "");
    }

    /// <summary>Looks for Homestead again (installed or moved since), without listing the blueprints.</summary>
    /// <returns>What was found, now <see cref="Status"/>.</returns>
    internal Homestead.Status LookAgain() => Status = FindHomestead();
}
