using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// Create world: the drawn world made in one of the game's world folders (its file carrying the map, which the game
/// reads with the DrawnWorlds mod), checked as New world checks its worlds.
/// </summary>
public sealed partial class DrawWorldPage
{
    /// <summary>Create, once checked: the world's folder, its name and seed, and the rules of its land.</summary>
    public event Action<string, string, string, ValheimGen.WorldRules>? CreateRequested;

    /// <summary>The world's name.</summary>
    internal TextBox NameBox { get; } = new TextBox { PlaceholderText = Lang.T("newWorld.hint.name"), FontSize = 13 }.Tip("newWorld.name");

    /// <summary>Which of the game's world folders the world goes in; its Tag holds the full paths.</summary>
    internal ComboBox Where { get; } = new()
    {
        HorizontalAlignment = HorizontalAlignment.Stretch,
        FontSize = 12,
        ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<string>((path, _) => new TextBlock { Text = path, TextTrimming = TextTrimming.PrefixCharacterEllipsis }),
    };

    /// <summary>Checks the name, then asks the window to make the world.</summary>
    internal Button Create { get; } = new Button { Content = Lang.T("newWorld.label.create"), FontSize = 13 }.Classed("primary").Tip("drawWorld.create");

    /// <summary>Why Create refused or failed, or empty.</summary>
    internal TextBlock Problem { get; } = new() { Foreground = new SolidColorBrush(Color.FromRgb(224, 96, 75)), TextWrapping = TextWrapping.Wrap, FontSize = 12 };

    /// <summary>The world folders offered (the installed game's first), and Create wired.</summary>
    /// <param name="roots">The folders (tests); null for the game's own.</param>
    private void WireCreate(IReadOnlyList<string>? roots)
    {
        var where = (roots ?? NewWorldPage.Roots()).ToList();
        if (where.Count == 0)
        {
            where.Add(Places.GameWorldRoot(AppSettings.Load()));
        }
        Where.ItemsSource = where.Select(Ui.Tilde).ToList();
        Where.Tag = where;
        Where.SelectedIndex = 0;
        Create.Click += async (_, _) => await AskCreate();
    }

    /// <summary>The world card's part for Create: name, folder, the button and why it refused.</summary>
    private StackPanel CreateRows() => new()
    {
        Spacing = 8,
        Children =
        {
            NewWorldPage.Row(Lang.T("dialog.label.name"), NameBox),
            NewWorldPage.Row(Lang.T("newWorld.label.savedIn"), Where),
            Create,
            Problem,
            Ui.Hint(Lang.T("drawWorld.hint.mod")),
        },
    };

    /// <summary>Create: the name checked here, the land laid out, then the world made by the window (CreateRequested).</summary>
    internal async Task AskCreate()
    {
        string name = (NameBox.Text ?? "").Trim(), seed = SeedName;
        var roots = (List<string>)Where.Tag!;
        string root = roots[Math.Max(0, Where.SelectedIndex)];
        if (NewWorldPage.Check(root, name, seed) is string problem)
        {
            Problem.Text = problem;
            return;
        }
        Problem.Text = "";
        _making?.Cancel();
        var map = Canvas.Map.Copy();
        Create.IsEnabled = false;
        try
        {
            var rules = await Task.Run(() => MasterMapWorlds.Rules(map));
            CreateRequested?.Invoke(Path.Combine(root, name), name, seed, rules);
        }
        finally
        {
            Create.IsEnabled = true;
        }
    }
}
