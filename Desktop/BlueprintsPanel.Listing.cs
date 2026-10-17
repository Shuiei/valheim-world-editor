using Avalonia;
using Avalonia.Controls;
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
        _homestead = Homestead.List(Status.Folder);
        _older = Store.List();
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
        _folder.Text = Lang.T("blueprints.folder", Ui.Tilde(Status.Folder)) + (_older.Count > 0 ? " " + Lang.T("blueprints.folderOlder", Ui.Tilde(Store.Directory)) : "");
        Render();
    }

    /// <summary>Looks for Homestead again (installed or moved since), without listing the blueprints.</summary>
    /// <returns>What was found, now <see cref="Status"/>.</returns>
    internal Homestead.Status LookAgain() => Status = FindHomestead();

    /// <summary>Fills the list with the blueprints that match the search: Homestead's first, then the older ones.</summary>
    private void Render()
    {
        List.Children.Clear();
        string q = Search.Text?.Trim() ?? "";
        bool Match(string name) => q == "" || name.Contains(q, StringComparison.OrdinalIgnoreCase);
        var homestead = _homestead.Where(e => e.Matches(q)).ToList();
        var older = _older.Where(b => Match(b.Name)).ToList();
        if (homestead.Count + older.Count == 0)
        {
            List.Children.Add(new TextBlock
            {
                Text = _homestead.Count + _older.Count > 0 ? Lang.T("blueprints.noMatch") : Lang.T("blueprints.empty"),
                FontSize = 11,
                Foreground = Ui.Muted,
                TextWrapping = TextWrapping.Wrap,
            });
            return;
        }
        foreach (var e in homestead)
        {
            string id = HomesteadId + Path.GetFileName(e.Path);
            string meta = Meta(
                Lang.T("blueprints.meta.pieces", e.Pieces),
                e.Creator is { Length: > 0 } c ? Lang.T("blueprints.meta.by", c) : null,
                e.World != null ? Lang.T("blueprints.meta.from", e.World) : null);
            var cost = Terrain.PieceCost.Of(e.Kinds);
            List.Children.Add(Row(BlueprintPictures.File(e.Picture), e.Name, meta, act =>
            {
                act(Lang.T("paste.label.paste"), "blueprints.paste", () => Paste(id));
                act(Lang.T("blueprints.label.edit"), "blueprints.edit", () => EditAsked?.Invoke(e.Path));
                act(Lang.T("blueprints.label.details"), "blueprints.details", async () => await EditDetails(e));
                act(VbuildLabel, "blueprints.vbuild", () => Export(id, "vbuild"));
                act(Lang.T("common.delete"), "blueprints.delete", async () => await Delete(id, e.Name));
            }, Lang.T("blueprints.cost", cost.Describe()), e.Description, e.Tags));
        }
        if (older.Count > 0)
        {
            List.Children.Add(new TextBlock { Text = Lang.T("blueprints.head.older"), FontSize = 11.5, FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 6, 0, 0) });
        }
        foreach (var b in older)
        {
            string meta = Meta(
                Lang.T("blueprints.meta.size", b.W, b.H),
                Lang.T("blueprints.meta.objects", b.Objects),
                b.Ground ? Lang.T("blueprints.meta.ground") : null,
                b.World != null ? Lang.T("blueprints.meta.from", b.World) : null,
                b.Source);
            List.Children.Add(Row(BlueprintPictures.Url(b.Thumb), b.Name, meta, act =>
            {
                act(Lang.T("paste.label.paste"), "blueprints.paste", () => Paste(b.Id));
                act(Lang.T("blueprints.label.toHomestead"), "blueprints.toHomestead", () => ToHomestead(b.Id));
                act(VbuildLabel, "blueprints.vbuildOld", () => Export(b.Id, "vbuild"));
                act(Lang.T("common.delete"), "blueprints.deleteOld", async () => await Delete(b.Id, b.Name));
            }));
        }
    }
}
