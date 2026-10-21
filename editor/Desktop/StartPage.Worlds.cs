using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class StartPage
{
    /// <summary>
    /// A card per world found on this computer (worlds that cannot be opened greyed out, with why), looked for away
    /// from the window's thread (<see cref="WorldsCheck"/>): a slow or broken folder neither freezes the page nor
    /// leaves it half set.
    /// </summary>
    internal void FillWorlds() => WorldsCheck = CheckWorlds(always: true);

    /// <summary>
    /// Looks for the worlds again away from the window's thread (a slow drive does not freeze it) and shows
    /// them when they changed: a world folder added or removed, or a world saved again. A look that a later
    /// one (Refresh, or the page shown again) overtook shows nothing: its worlds are older.
    /// </summary>
    /// <param name="always">Shows them even when they did not change, or another page shows (the list filled).</param>
    internal async Task CheckWorlds(bool always = false)
    {
        int look = ++_worldsLook;
        List<Worlds.Info> worlds;
        try
        {
            worlds = await Task.Run(() => FindWorlds(_settings));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            // A folder changing while it is read (or the settings while they are walked): the next look sees it (brief:
            // looked at every few seconds).
            Log.CaughtBrief(ex, "looking for saved worlds");
            return;
        }
        if (look == _worldsLook && (always || Mode == "offline" && WorldsKey(worlds) != _worldsKey))
        {
            ShowWorlds(worlds);
        }
    }

    /// <summary>What a list of worlds shows (every detail on a card), to compare one look with the last.</summary>
    private static string WorldsKey(List<Worlds.Info> worlds) =>
        string.Join('\n', worlds.Select(w => $"{w.Path}|{w.Name}|{w.Where}|{w.SaveNumber}|{w.Saved.Ticks}|{w.Usable}|{w.Problem}|{w.ExpandWorld}|{w.Generator}"));

    /// <summary>
    /// Where a world was found (<see cref="Worlds.Info.Where"/>: "recent", "yours", "local") as the user reads it; another
    /// value as it is.
    /// </summary>
    /// <param name="where">The value.</param>
    internal static string WhereName(string where) => where switch
    {
        "recent" => Lang.T("start.where.recent"),
        "yours" => Lang.T("start.where.yours"),
        "local" => Lang.T("start.where.local"),
        _ => where,
    };

    /// <summary>
    /// A card's last line: where the world was found, then the world generation mods it was made with (Riverheim,
    /// Expand World).
    /// </summary>
    /// <param name="w">The world.</param>
    internal static string MadeWith(Worlds.Info w)
    {
        string text = WhereName(w.Where);
        if (w.Generator is { } generator)
        {
            text = Lang.T("start.world.generator", text, generator);
        }
        return w.ExpandWorld ? Lang.T("start.world.expandWorld", text) : text;
    }

    /// <summary>A card per world given.</summary>
    private void ShowWorlds(List<Worlds.Info> worlds)
    {
        _worldsKey = WorldsKey(worlds);
        WorldCards.Children.Clear();
        if (worlds.Count == 0)
        {
            WorldCards.Children.Add(Hint(Lang.T("start.hint.noWorlds")));
            return;
        }
        foreach (var w in worlds)
        {
            var card = new Button
            {
                Width = 300,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Padding = Ui.Pad,
                IsEnabled = w.Usable,
                Content = new StackPanel
                {
                    Spacing = 2,
                    Children =
                    {
                        new TextBlock { Text = w.Name, FontSize = 15, FontWeight = FontWeight.SemiBold },
                        new TextBlock { Text = w.Usable ? Lang.T("start.world.saved", w.Saved, w.SaveNumber) : w.Problem ?? "", FontSize = 12, Foreground = w.Usable ? Muted : Brushes.Orange, TextWrapping = TextWrapping.Wrap },
                        new TextBlock { Text = MadeWith(w), FontSize = 11, Foreground = Muted, TextTrimming = TextTrimming.CharacterEllipsis },
                    },
                },
            };
            ToolTip.SetTip(card, $"{Tips.Of("start.world")}\n{Ui.Tilde(w.Path)}");
            string path = w.Path;
            card.Click += (_, _) => OpenFolder(path);
            WorldCards.Children.Add(card);
        }
    }
}
