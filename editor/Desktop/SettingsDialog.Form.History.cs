using System.Globalization;
using Avalonia.Controls;
using Avalonia.Layout;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public static partial class SettingsDialog
{
    internal sealed partial class Form
    {
        private TextBox _historySteps = null!;

        /// <summary>Builds the History section: how many steps the history keeps (empty or 0: no limit).</summary>
        private StackPanel BuildHistory()
        {
            _historySteps = new TextBox
            {
                Name = "HistorySteps",
                Text = _settings.HistorySteps > 0 ? _settings.HistorySteps.ToString(CultureInfo.InvariantCulture) : "",
                PlaceholderText = Lang.T("settings.historyNoLimit"),
                Width = 120,
            }.Tip("settings.historySteps");
            return new StackPanel
            {
                Spacing = 6,
                Children =
                {
                    H2(Lang.T("settings.history")),
                    new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { new TextBlock { Text = Lang.T("settings.historyStepsLabel"), VerticalAlignment = VerticalAlignment.Center }, _historySteps } },
                    Hint(Lang.T("settings.historyHint")),
                },
            };
        }

        /// <summary>The steps typed: empty is 0 (no limit); null when not a whole number from 0 up.</summary>
        private int? ReadHistorySteps()
        {
            string text = _historySteps.Text?.Trim() ?? "";
            if (text.Length == 0)
            {
                return 0;
            }
            return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out int steps) ? steps : null;
        }
    }
}
