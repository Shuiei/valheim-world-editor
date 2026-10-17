using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using TerrainEditor.App;
using TerrainEditor.Save;

namespace TerrainEditor.Desktop;

public sealed partial class StartPage
{
    /// <summary>
    /// The game's card in My game: the running game with its world, players and the button to edit it, or the steps to take
    /// to start it, as the check found it.
    /// </summary>
    /// <param name="running">The running game, or null when none runs.</param>
    /// <param name="state">The state the check found (running, installed, plugin-not-started, no-plugin or no-bepinex).</param>
    /// <param name="where">Where the game's plugin is (empty when unknown).</param>
    /// <returns>The card's content.</returns>
    private Control GameContent(LocalGame.Running? running, string state, string where)
    {
        if (running != null)
        {
            var edit = EditLiveButton(running);
            return new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                Children =
                {
                    new StackPanel { Children = { new TextBlock { Text = Lang.T("start.game.running", running.World), FontSize = 15, FontWeight = FontWeight.SemiBold }, Hint(where != "" ? Lang.T("start.game.playersAt", running.Players, where) : Lang.T("start.game.players", running.Players)) } },
                    Ui.Col(edit, 1),
                },
            };
        }
        else if (state == "installed")
        {
            return new StackPanel { Children = { new TextBlock { Text = Lang.T("start.game.startValheim"), FontSize = 15, FontWeight = FontWeight.SemiBold }, Hint(where != "" ? Lang.T("start.game.installedAt", where) : Lang.T("start.game.installed")) } };
        }
        else if (state == "plugin-not-started")
        {
            return new StackPanel { Children = { new TextBlock { Text = Lang.T("start.game.startOnce"), FontSize = 15, FontWeight = FontWeight.SemiBold }, Hint(Lang.T("start.game.startOnceHint")) } };
        }
        else
        {
            return new StackPanel
            {
                Spacing = 6,
                Children =
                {
                    new TextBlock { Text = Lang.T("start.game.needs"), FontSize = 15, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap },
                    Hint(state == "no-plugin" ? Lang.T("start.game.bepInExThere") : ""),
                    new TextBlock { Text = Lang.T("start.game.step1"), TextWrapping = TextWrapping.Wrap },
                    new TextBlock { Text = Lang.T("start.game.step2"), TextWrapping = TextWrapping.Wrap },
                    new TextBlock { Text = Lang.T("start.game.step3"), TextWrapping = TextWrapping.Wrap },
                },
            };
        }
    }

    /// <summary>The Edit live button for the game running with the plugin: opens its world live through the bridge.</summary>
    /// <param name="running">The running game.</param>
    private Button EditLiveButton(LocalGame.Running running)
    {
        var edit = new Button { Content = Lang.T("start.label.editLive"), VerticalAlignment = VerticalAlignment.Center }.Classed("primary").Tip("start.editLive");
        var bridge = running.Bridge;
        edit.Click += (_, _) => OpenLive(new LiveBridge($"http://127.0.0.1:{bridge.Port}", bridge.Token), "my game", "game", Lang.T("start.msg.connectingGame"));
        return edit;
    }

    /// <summary>Home's card when the game runs with the plugin: its world, and Edit live.</summary>
    private Border RunningCard(LocalGame.Running running, string where)
    {
        var edit = EditLiveButton(running);
        edit.FontSize = 13;
        return new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(31, 42, 34)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(62, 107, 69)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(18, 14),
            MaxWidth = 900,
            HorizontalAlignment = HorizontalAlignment.Left,
            Child = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                ColumnSpacing = 20,
                Children =
                {
                    new StackPanel { Children = { new TextBlock { Text = Lang.T("start.game.running", running.World), FontSize = 15, FontWeight = FontWeight.SemiBold }, Hint(where != "" ? Lang.T("start.game.playersLiveAt", running.Players, where) : Lang.T("start.game.playersLive", running.Players)) } },
                    Ui.Col(edit, 1),
                },
            },
        };
    }
}
