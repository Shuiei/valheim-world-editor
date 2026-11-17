using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using TerrainEditor.App;
namespace TerrainEditor.Desktop;

internal sealed partial class BlueprintLibrary
{
    /// <summary>Fills the list with the blueprints that match the search: Homestead's first, then the older ones.</summary>
    /// <param name="again">Reads the folders again; false (the search changed): the blueprints read last are searched.</param>
    internal void Render(bool again = true)
    {
        string folder = Folder();
        if (again || _read is not { } read || read.Folder != folder)
        {
            _read = read = (folder, Homestead.List(folder), Older()?.List() ?? []);
        }
        List.Children.Clear();
        _wholeLines.Clear();
        string q = Search.Text?.Trim() ?? "";
        var homestead = read.Homestead.Where(e => e.Matches(q)).ToList();
        var older = read.Older.Where(b => q == "" || b.Name.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
        if (homestead.Count + older.Count == 0)
        {
            List.Children.Add(new TextBlock
            {
                Text = read.Homestead.Count + read.Older.Count > 0 ? Lang.T("blueprints.noMatch")
                    : Place == Places.Workshop ? Lang.T("build.libraryEmpty") : Lang.T("blueprints.empty"),
                FontSize = 11,
                Foreground = Ui.Muted,
                TextWrapping = TextWrapping.Wrap,
            });
            return;
        }
        foreach (var e in homestead)
        {
            List.Children.Add(HomesteadRow(e));
        }
        if (older.Count > 0)
        {
            List.Children.Add(WholeLine(new TextBlock { Text = Lang.T("blueprints.head.older"), FontSize = 11.5, FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 6, 0, 0) }));
        }
        foreach (var b in older)
        {
            List.Children.Add(OlderRow(b));
        }
    }

    /// <summary>A child of the list that takes a whole line of it, whatever the list's width.</summary>
    private Control WholeLine(Control c)
    {
        _wholeLines.Add(c);
        if (List.Bounds.Width > 0)
        {
            c.Width = List.Bounds.Width;
        }
        return c;
    }

    /// <summary>A row's meta line: its parts that are there, separated by " · ".</summary>
    /// <param name="parts">The parts, in order (null: left out).</param>
    internal static string Meta(params string?[] parts) => string.Join(" · ", parts.Where(p => p != null));

    /// <summary>
    /// A Homestead blueprint's row: its pieces, who made it, the world it came from and how many of its pieces only the
    /// editor can place (when its file says), its cost, description and tags; put down, opened in the Workshop, its
    /// details changed, written as .vbuild, deleted.
    /// </summary>
    private Border HomesteadRow(Homestead.Entry e)
    {
        var item = new Item(IdOf(e), e.Name, e);
        string meta = Meta(
            Lang.T("blueprints.meta.pieces", e.Pieces),
            e.Creator is { Length: > 0 } c ? Lang.T("blueprints.meta.by", c) : null,
            e.World != null ? Lang.T("blueprints.meta.from", e.World) : null,
            e.EditorOnlyNote?.Shown);
        var picture = BlueprintPictures.File(e.Picture);
        var row = Row(item, picture, meta, act =>
        {
            if (Place == Places.Workshop)
            {
                act(Lang.T("common.add"), "build.add", () => PlaceAsked?.Invoke(item));
                act(Lang.T("common.open"), "build.open", () => OpenAsked?.Invoke(item));
            }
            else
            {
                act(Lang.T("paste.label.paste"), "blueprints.paste", () => PlaceAsked?.Invoke(item));
                act(Lang.T("common.open"), "blueprints.open", () => OpenAsked?.Invoke(item));
            }
            act(Lang.T("blueprints.label.details"), "blueprints.details", () => DetailsAsked?.Invoke(item));
            act(VbuildLabel, "blueprints.vbuild", () => ExportAsked?.Invoke(item));
            act(Lang.T("common.delete"), "blueprints.delete", () => DeleteAsked?.Invoke(item));
        }, Lang.T("blueprints.cost", Homestead.Cost(e.Kinds).Describe()), e.Description, e.Tags);
        if (picture == null)
        {
            DrawPicture(e.Path, PictureOf(row));
        }
        if (Place == Places.Workshop)
        {
            Draggable(row, e.Path);
        }
        return row;
    }

    /// <summary>
    /// An older editor blueprint's row: its size, objects, whether it holds ground, where it came from; pasted (in a
    /// world), made a Homestead blueprint, written as .vbuild, deleted.
    /// </summary>
    private Border OlderRow(BlueprintStore.Summary b)
    {
        var item = new Item(b.Id, b.Name, null);
        string meta = Meta(
            Lang.T("blueprints.meta.size", b.W, b.H),
            Lang.T("blueprints.meta.objects", b.Objects),
            b.Ground ? Lang.T("blueprints.meta.ground") : null,
            b.World != null ? Lang.T("blueprints.meta.from", b.World) : null,
            b.Source);
        return Row(item, BlueprintPictures.Url(b.Thumb), meta, act =>
        {
            if (Place == Places.World)
            {
                act(Lang.T("paste.label.paste"), "blueprints.paste", () => PlaceAsked?.Invoke(item));
            }
            act(Lang.T("blueprints.label.toHomestead"), "blueprints.toHomestead", () => ToHomesteadAsked?.Invoke(item));
            act(VbuildLabel, "blueprints.vbuildOld", () => ExportAsked?.Invoke(item));
            act(Lang.T("common.delete"), "blueprints.deleteOld", () => DeleteAsked?.Invoke(item));
        });
    }
}
