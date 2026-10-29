using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

// The list of kinds by category: each folds, and its kinds are made when it opens.
public sealed partial class PlacePanel
{
    /// <summary>
    /// Fills the list of kinds (while it shows), filtered by the search: a folding category per kind of object, its
    /// checkboxes made when it opens.
    /// </summary>
    public void FillList()
    {
        if (!Chooser.IsVisible)
        {
            return;
        }
        string q = Search.Text?.Trim() ?? "";
        List.Children.Clear();
        Groups.Clear();
        foreach (var k in Order)
        {
            var names = Creatable().Where(c => c.Kind == k && (q == "" || Matches(c.Name, q))).Select(c => c.Name).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
            if (names.Count == 0)
            {
                continue;
            }
            AddCategory(k, names, q);
        }
        if (List.Children.Count == 0)
        {
            List.Children.Add(new TextBlock { Text = Lang.T("blueprints.noMatch"), FontSize = 11, Foreground = Ui.Muted });
        }
    }

    /// <summary>
    /// One category of kinds that folds: its header counts the chosen kinds, and its checkboxes are made when it opens.
    /// </summary>
    /// <param name="k">The category.</param>
    /// <param name="names">Its kinds, in the list's order.</param>
    /// <param name="q">The search text (empty when none): searching opens every category with a match.</param>
    private void AddCategory(ObjectKind k, List<string> names, string q)
    {
        // One category that folds; searching opens every category with a match (not remembered).
        var body = new StackPanel { Spacing = 1 };
        var g = new Expander { HorizontalAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(2), IsExpanded = q != "" || OpenKinds.Contains(k), Content = body }.Tip("place.group");
        void Header() => g.Header = CategoryHeader(k, names);
        Header();
        // The checkboxes are made when the category opens (some hold hundreds of kinds).
        void Fill()
        {
            if (body.Children.Count > 0 || !g.IsExpanded)
            {
                return;
            }
            foreach (string n in names)
            {
                body.Children.Add(KindRow(n, Header));
            }
        }
        Fill();
        g.PropertyChanged += (_, e) =>
        {
            if (e.Property != Expander.IsExpandedProperty)
            {
                return;
            }
            Fill();
            if (q != "")
            {
                return;
            }
            if (g.IsExpanded ? OpenKinds.Add(k) : OpenKinds.Remove(k))
            {
                OpenKindsChanged?.Invoke();
            }
        };
        Groups[k] = g;
        List.Children.Add(g);
    }
}
